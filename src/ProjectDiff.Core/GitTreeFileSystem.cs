using System.IO.Enumeration;
using System.Xml;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.FileSystem;

namespace ProjectDiff.Core;

internal sealed class GitTreeFileSystem : MSBuildFileSystemBase
{
    private readonly DirectoryInfo _directory;
    private readonly Tree _tree;
    private readonly ProjectCollection _projectCollection;
    private readonly Dictionary<string, string> _globalProperties;

    public GitTreeFileSystem(
        DirectoryInfo directory,
        Tree tree,
        ProjectCollection projectCollection,
        Dictionary<string, string> globalProperties
    )
    {
        _directory = directory;
        _tree = tree;
        _projectCollection = projectCollection;
        _globalProperties = globalProperties;
    }

    public override TextReader ReadFile(string path)
    {
        throw new NotSupportedException("ReadFile");
    }

    public override Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        if (!ShouldUseTree(path))
        {
            return base.GetFileStream(path, mode, access, share);
        }

        if (mode != FileMode.Open)
        {
            throw new ArgumentException("Only reading files is supported", nameof(mode));
        }

        if (access != FileAccess.Read)
        {
            throw new ArgumentException("Only reading files is supported", nameof(access));
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
        throw new NotSupportedException("ReadFileAllText");
    }

    public override byte[] ReadFileAllBytes(string path)
    {
        throw new NotSupportedException("ReadFileAllBytes");
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


        var entries = EnumerateTreeFileSystemEntries(path, searchPattern, TreeEntryTargetType.Blob);

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            return entries;
        }


        return entries.Concat(
            EnumerateDirectories(path)
                .SelectMany(dir => EnumerateFiles(dir, searchPattern, searchOption))
        );
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

        if (searchOption != SearchOption.TopDirectoryOnly)
        {
            throw new NotSupportedException("EnumerateDirectories");
        }


        return EnumerateTreeFileSystemEntries(path, searchPattern, TreeEntryTargetType.Tree);
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

        if (searchOption != SearchOption.TopDirectoryOnly)
        {
            throw new NotSupportedException("EnumerateFileSystemEntries");
        }

        return EnumerateTreeFileSystemEntries(path, searchPattern, null);
    }

    private IEnumerable<string> EnumerateTreeFileSystemEntries(
        string path,
        string searchPattern,
        TreeEntryTargetType? targetType
    )
    {
        var p = RelativePath(path);

        var entry = _tree[p];
        if (entry == null)
        {
            yield break;
        }

        if (entry.TargetType != TreeEntryTargetType.Tree)
        {
            yield break;
        }

        var treeEntry = (Tree)entry.Target;

        foreach (var e in treeEntry)
        {
            if (targetType.HasValue && e.TargetType != targetType)
            {
                continue;
            }

            if (!FileSystemName.MatchesWin32Expression(searchPattern, e.Name))
            {
                continue;
            }


            var fullPath = Path.GetFullPath(e.Path, _directory.FullName);

            yield return fullPath;
        }
    }

    public override FileAttributes GetAttributes(string path)
    {
        throw new NotSupportedException("GetAttributes");
    }

    public override DateTime GetLastWriteTimeUtc(string path)
    {
        throw new NotSupportedException("GetLastWriteTimeUtc");
    }

    public override bool DirectoryExists(string path)
    {
        if (!ShouldUseTree(path))
            return base.DirectoryExists(path);

        var entry = _tree[RelativePath(path)];

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

        if (!IsProject(entry))
        {
            return true;
        }

        // HACK: Since Imports doesn't use the file system we have to manually load the projects
        // whenever msbuild tries to load them.
        lock (_projectLoadLock)
        {
            if (_projectCollection.GetLoadedProjects(path).Count == 0)
            {
                LoadProject(path, _globalProperties, _projectCollection);
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
        Path.GetRelativePath(_directory.FullName, path)
            .Replace('\\', '/');

    private bool ShouldUseTree(string path) =>
        path.StartsWith(_directory.FullName);

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

        var relativePath = RelativePath(path);

        var entry = _tree[relativePath];

        if (entry is null || entry.TargetType != TreeEntryTargetType.Blob)
        {
            throw new NotImplementedException();
        }

        var blob = (Blob)entry.Target;

        using var xml = new XmlTextReader(new StringReader(blob.GetContentText()));
        var projectRootElement = ProjectRootElement.Create(xml, projects);
        projectRootElement.FullPath = path;

        return Project.FromProjectRootElement(
            projectRootElement,
            new ProjectOptions
            {
                GlobalProperties = globalProperties,
                ProjectCollection = projects,
                LoadSettings = ProjectLoadSettings.Default | ProjectLoadSettings.RecordDuplicateButNotCircularImports
            }
        );
    }
}