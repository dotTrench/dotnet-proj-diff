using System.IO.Enumeration;
using System.Xml;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.FileSystem;
using Microsoft.Extensions.Logging;

namespace ProjectDiff.Core;

public sealed class GitTreeFileSystem : MSBuildFileSystemBase
{
    private readonly Repository _repository;
    private readonly Tree _tree;
    private readonly ProjectCollection _projectCollection;
    private readonly Dictionary<string, string> _globalProperties;
    private readonly ILogger<GitTreeFileSystem> _logger;

    public GitTreeFileSystem(
        Repository repository,
        Tree tree,
        ProjectCollection projectCollection,
        Dictionary<string, string> globalProperties,
        ILogger<GitTreeFileSystem> logger
    )
    {
        _repository = repository;
        _tree = tree;
        _projectCollection = projectCollection;
        _globalProperties = globalProperties;
        _logger = logger;
    }

    public bool EagerLoadProjects { get; set; }

    public override TextReader ReadFile(string path)
    {
        if (!ShouldUseTree(path))
        {
            return base.ReadFile(path);
        }

        var stream = GetFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new StreamReader(stream);
    }

    public override Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        if (!ShouldUseTree(path))
        {
            return base.GetFileStream(path, mode, access, share);
        }

        if (mode != FileMode.Open)
        {
            throw new NotSupportedException("Mode must be FileMode.Open");
        }

        if (access != FileAccess.Read)
        {
            throw new NotSupportedException("Access mode must be FileAccess.Read");
        }

        var relativePath = RelativePath(path);
        var entry = _tree[relativePath];
        if (entry == null || entry.TargetType != TreeEntryTargetType.Blob)
        {
            throw new InvalidOperationException("Tried reading a file that is not a blob");
        }

        return entry.Target.Peel<Blob>().GetContentStream();
    }

    public override string ReadFileAllText(string path)
    {
        if (!ShouldUseTree(path))
        {
            return base.ReadFileAllText(path);
        }

        using var stream = GetFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public override byte[] ReadFileAllBytes(string path)
    {
        if (!ShouldUseTree(path))
        {
            return base.ReadFileAllBytes(path);
        }

        using var stream = GetFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public override IEnumerable<string> EnumerateFiles(
        string path,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        if (!ShouldUseTree(path))
        {
            return base.EnumerateFiles(path, searchPattern, searchOption);
        }

        return EnumerateTree(path, searchPattern, searchOption, TreeEntryTargetType.Blob);
    }


    public override IEnumerable<string> EnumerateDirectories(
        string path,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        if (!ShouldUseTree(path))
        {
            return base.EnumerateDirectories(path, searchPattern, searchOption);
        }

        return EnumerateTree(path, searchPattern, searchOption, TreeEntryTargetType.Tree);
    }


    public override IEnumerable<string> EnumerateFileSystemEntries(
        string path,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly
    )
    {
        if (!ShouldUseTree(path))
        {
            return base.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        return EnumerateTree(path, searchPattern, searchOption, null);
    }

    public override FileAttributes GetAttributes(string path)
    {
        throw new NotSupportedException(nameof(GetAttributes));
    }

    public override DateTime GetLastWriteTimeUtc(string path)
    {
        throw new NotSupportedException(nameof(GetLastWriteTimeUtc));
    }

    public override bool DirectoryExists(string path)
    {
        if (!ShouldUseTree(path))
            return base.DirectoryExists(path);

        var relativePath = RelativePath(path);
        var entry = _tree[relativePath];

        return entry is { TargetType: TreeEntryTargetType.Tree };
    }

    private readonly object _projectLoadLock = new();

    public override bool FileExists(string path)
    {
        if (!ShouldUseTree(path))
            return base.FileExists(path);

        var entry = _tree[RelativePath(path)];
        if (entry is null)
        {
            return false;
        }

        if (entry.TargetType != TreeEntryTargetType.Blob)
        {
            return false;
        }

        if (EagerLoadProjects && IsProject(entry))
        {
            // HACK: Since Imports doesn't use the file system we have to manually load the projects
            // whenever msbuild tries to load them.
            lock (_projectLoadLock)
            {
                if (_projectCollection.GetLoadedProjects(path).Count == 0)
                {
                    _logger.LogDebug("Eagerly loading project from path '{Path}'", path);
                    LoadProject(path, _globalProperties, _projectCollection);
                }
            }
        }

        return true;
    }

    private static bool IsProject(TreeEntry entry)
    {
        var extension = Path.GetExtension(entry.Path);

        if (extension is ".props" or ".targets")
            return true;

        return false;
    }

    public override bool FileOrDirectoryExists(string path)
    {
        if (!ShouldUseTree(path))
            return base.FileOrDirectoryExists(path);


        var entry = _tree[RelativePath(path)];

        return entry is not null;
    }

    private string RelativePath(string path) =>
        Path.GetRelativePath(_repository.Info.WorkingDirectory, path)
            .Replace('\\', '/');

    private bool ShouldUseTree(string path) =>
        path.StartsWith(_repository.Info.WorkingDirectory);

    public Project LoadProject(
        string path,
        Dictionary<string, string> globalProperties,
        ProjectCollection projects
    )
    {
        if (projects != _projectCollection)
        {
            throw new NotImplementedException();
        }

        _logger.LogDebug("Loading project from path '{Path}'", path);
        var relativePath = RelativePath(path);

        var entry = _tree[relativePath];

        if (entry is null || entry.TargetType != TreeEntryTargetType.Blob)
        {
            throw new InvalidOperationException("Tried loading a project that is not a blob");
        }

        var blob = entry.Target.Peel<Blob>();

        using var xml = new XmlTextReader(new StringReader(blob.GetContentText()));
        var projectRootElement = ProjectRootElement.Create(xml, projects);
        projectRootElement.FullPath = path;

        var project = Project.FromProjectRootElement(
            projectRootElement,
            new ProjectOptions
            {
                GlobalProperties = globalProperties,
                ProjectCollection = projects,
                LoadSettings = ProjectLoadSettings.Default | ProjectLoadSettings.RecordDuplicateButNotCircularImports
            }
        );

        _logger.LogDebug("Loaded project '{ProjectName}' from git tree '{Tree}'", project.FullPath, _tree.Sha);
        return project;
    }


    private IEnumerable<string> EnumerateTree(
        string path,
        string searchPattern,
        SearchOption searchOption,
        TreeEntryTargetType? targetType
    )
    {
        var (tree, treePath) = FindTree(path);
        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            return tree
                .Where(ShouldInclude)
                .Select(it => Path.GetFullPath(Path.Combine(treePath, it.Path)));
        }

        return ExpandTree(tree, treePath)
            .Where(e => ShouldInclude(e.Entry))
            .Select(it => it.Path);

        bool ShouldInclude(TreeEntry entry)
        {
            if (targetType.HasValue && entry.TargetType != targetType)
            {
                return false;
            }

            return FileSystemName.MatchesWin32Expression(searchPattern, entry.Name);
        }
    }

    private static IEnumerable<(string Path, TreeEntry Entry)> ExpandTree(Tree tree, string parentPath)
    {
        foreach (var entry in tree)
        {
            var fullPath = Path.Combine(parentPath, entry.Path);

            yield return (fullPath, entry);

            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                var subTree = entry.Target.Peel<Tree>();
                foreach (var subEntry in ExpandTree(subTree, fullPath))
                {
                    yield return subEntry;
                }
            }
        }
    }

    private (Tree Tree, string Path) FindTree(string path)
    {
        var relativePath = RelativePath(path);
        if (relativePath == ".")
        {
            return (_tree, _repository.Info.WorkingDirectory);
        }

        var entry = _tree[relativePath];
        if (entry == null || entry.TargetType != TreeEntryTargetType.Tree)
        {
            throw new InvalidOperationException("Tried to enumerate files in a path that is not a tree");
        }

        return (entry.Target.Peel<Tree>(),
            Path.GetFullPath(Path.Combine(_repository.Info.WorkingDirectory, relativePath)));
    }
}
