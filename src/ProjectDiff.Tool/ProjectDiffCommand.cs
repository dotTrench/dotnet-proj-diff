using System.Collections.Frozen;
using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using ProjectDiff.Core;

namespace ProjectDiff.Tool;

public sealed class ProjectDiffCommand : RootCommand
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter<DiffStatus>()
            }
        };

    private readonly Argument<FileInfo> _solutionArgument = new(
        "solution",
        "Path to solution file to derive projects from"
    )
    {
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Argument<string> _commitArgument = new(
        "commit",
        () => "HEAD",
        "Base git reference to compare against"
    )
    {
        Arity = ArgumentArity.ZeroOrOne
    };

    private readonly Option<bool> _mergeBaseOption = new(
        "--merge-base",
        () => true,
        "If true instead of using <commit> use the merge base of <commit> and HEAD"
    );

    private readonly Option<bool> _includeDeleted = new(
        "--include-deleted",
        () => false,
        "If true deleted projects will be included in output"
    );

    private readonly Option<bool> _includeModified = new(
        "--include-modified",
        () => true,
        "If true modified projects will be included in output"
    );

    private readonly Option<bool> _includeAdded = new(
        "--include-added",
        () => true,
        "If true added projects will be included in output"
    );

    private readonly Option<bool> _includeReferencing = new(
        "--include-referencing",
        () => true,
        "if true  projects referencing modified/deleted/added projects will be included in output"
    );

    private readonly Option<OutputFormat?> _format = new(
        "--format",
        "Output format, if --output is specified format will be derived from file extension. Otherwise this defaults to 'plain'"
    );

    private readonly Option<bool> _absolutePaths = new(
        "--absolute-paths",
        () => false,
        """
        Output absolute paths, if not specified paths will be relative to the working directory. 
        Or relative to --output if specified. This option will not affect slnf output as this requires relative paths
        """
    );

    private readonly Option<FileInfo?> _outputOption = new(
        "--output",
        "Output file, if not set stdout will be used"
    );

    public ProjectDiffCommand()
    {
        Name = "dotnet-proj-diff";

        _solutionArgument.AddValidator(
            x =>
            {
                var f = x.GetValueOrDefault<FileInfo?>();
                if (f is null)
                {
                    x.ErrorMessage = $"{x.Argument.Name} must be specified";
                }
                else if (!f.Exists)
                {
                    x.ErrorMessage = $"File '{f.FullName}' does not exist.";
                }
                else if (f.Extension is not (".sln" or ".slnx"))
                {
                    x.ErrorMessage = $"File '{f.FullName}' is not a valid sln file.";
                }
            }
        );
        AddArgument(_solutionArgument);
        AddArgument(_commitArgument);
        AddOption(_mergeBaseOption);
        AddOption(_includeDeleted);
        AddOption(_includeModified);
        AddOption(_includeAdded);
        AddOption(_includeReferencing);
        AddOption(_absolutePaths);
        AddOption(_format);
        AddOption(_outputOption);
        Handler = CommandHandler.Create(ExecuteAsync);
    }


    private async Task<int> ExecuteAsync(
        string commit,
        FileInfo solution,
        bool mergeBase,
        bool includeDeleted,
        bool includeModified,
        bool includeAdded,
        bool includeReferencing,
        bool absolutePaths,
        OutputFormat? format,
        FileInfo? output,
        IConsole c,
        CancellationToken cancellationToken
    )
    {
        if (c is not IExtendedConsole console)
        {
            throw new InvalidOperationException("Expected IExtendedConsole");
        }

        if (solution.Directory is null)
        {
            WriteError(console, $"Could resolve parent directory for solution '{solution.FullName}'");
            return 1;
        }

        var solutionDirectory = solution.Directory;
        var repoPath = Repository.Discover(solutionDirectory.FullName);
        if (repoPath is null)
        {
            WriteError(console, "Repository could not be found");
            return 1;
        }

        using var repo = new Repository(repoPath);
        var baseCommit = repo.Lookup<Commit>(commit);
        if (baseCommit is null)
        {
            WriteError(console, $"Could not find base reference {commit}");
            return 1;
        }

        var changes = GetGitModifiedFiles(repo, baseCommit, null).ToFrozenSet();
        if (changes.Count == 0)
        {
            return 0;
        }

        if (mergeBase)
        {
            var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(baseCommit, repo.Head.Tip);
            if (mergeBaseCommit is null)
            {
                WriteError(
                    console,
                    $"Could not find merge base reference for {baseCommit.Sha} and {repo.Head.Tip.Sha}"
                );
                return 1;
            }

            baseCommit = mergeBaseCommit;
        }


        var toGraph = await ProjectGraphFactory.BuildForWorkingDirectory(
            solution,
            cancellationToken
        );

        var fromGraph = await ProjectGraphFactory.BuildForGitTree(
            repo,
            baseCommit.Tree,
            solution,
            cancellationToken
        );

        var fromBuildGraph = BuildGraphFactory.CreateForProjectGraph(fromGraph, file => changes.Contains(file));
        var toBuildGraph = BuildGraphFactory.CreateForProjectGraph(toGraph, file => changes.Contains(file));

        var diff = BuildGraphDiff.Diff(fromBuildGraph, toBuildGraph, changes).Where(ShouldInclude);

        var diffOutput = new DiffOutput(output, console);
        OutputFormat outputFormat;
        if (format is not null)
        {
            outputFormat = format.Value;
        }
        else if (output is not null)
        {
            outputFormat = GetOutputFormatFromExtension(output.Extension);
        }
        else
        {
            outputFormat = OutputFormat.Plain;
        }

        switch (outputFormat)
        {
            case OutputFormat.Plain:
                await WritePlain(diffOutput, diff, absolutePaths);
                break;
            case OutputFormat.Json:
                await WriteJson(diffOutput, diff, absolutePaths);
                break;
            case OutputFormat.Slnf:
                await WriteSlnf(diffOutput, solution, diff);
                break;
            case OutputFormat.Traversal:
                await WriteTraversal(diffOutput, diff, absolutePaths);
                break;
            default:
                WriteError(console, $"Unknown output format {format}");
                return 1;
        }

        return 0;

        bool ShouldInclude(DiffProject project) =>
            project.Status switch
            {
                DiffStatus.Removed when includeDeleted => true,
                DiffStatus.Added when includeAdded => true,
                DiffStatus.Modified when includeModified => true,
                DiffStatus.ReferenceChanged when includeReferencing => true,
                _ => false
            };
    }

    private static async Task WritePlain(
        DiffOutput output,
        IEnumerable<DiffProject> diff,
        bool absolutePaths
    )
    {
        await using var stream = output.Open();
        await using var writer = new StreamWriter(stream);
        foreach (var project in diff)
        {
            var path = NormalizePath(
                output.RootDirectory,
                project.Path,
                absolutePaths
            );
            await writer.WriteLineAsync(path);
        }
    }

    private static async Task WriteJson(
        DiffOutput output,
        IEnumerable<DiffProject> diff,
        bool absolutePaths
    )
    {
        diff = diff.Select(
            project => project with
            {
                Path = NormalizePath(
                    output.RootDirectory,
                    project.Path,
                    absolutePaths
                )
            }
        );

        await using var stream = output.Open();
        await JsonSerializer.SerializeAsync(stream, diff, SerializerOptions);
    }

    private static string NormalizePath(string directory, string path, bool absolutePaths) =>
        absolutePaths ? path.Replace('\\', '/') : Path.GetRelativePath(directory, path).Replace('\\', '/');

    private static async Task WriteSlnf(
        DiffOutput output,
        FileInfo solution,
        IEnumerable<DiffProject> diff
    )
    {
        var solutionObject = new JsonObject
        {
            {
                "path", Path.GetRelativePath(
                    output.RootDirectory,
                    solution.FullName
                )
            }
        };
        var projects = new JsonArray();
        foreach (var project in diff)
        {
            var projectPath = Path.GetRelativePath(
                solution.Directory!.FullName,
                project.Path
            );
            projects.Add(projectPath);
        }

        solutionObject.Add("projects", projects);

        var root = new JsonObject { { "solution", solutionObject } };

        await using var stream = output.Open();
        await JsonSerializer.SerializeAsync(stream, root, SerializerOptions);
    }

    private static async Task WriteTraversal(
        DiffOutput output,
        IEnumerable<DiffProject> diff,
        bool absolutePaths
    )
    {
        var element = ProjectRootElement.Create(NewProjectFileOptions.None);
        element.Sdk = "Microsoft.Build.Traversal";
        foreach (var project in diff)
        {
            var path = !absolutePaths
                ? Path.GetRelativePath(output.RootDirectory, project.Path).Replace('/', '\\')
                : project.Path.Replace('/', '\\');

            element.AddItem("ProjectReference", path);
        }

        await using var stream = output.Open();
        await using var writer = new StreamWriter(stream);
        element.Save(writer);
    }

    private static void WriteError(IConsole console, string error)
    {
        console.Error.WriteLine(error);
    }

    private static IEnumerable<string> GetGitModifiedFiles(Repository repository, Commit baseCommit, Commit? headCommit)
    {
        using var changes = headCommit is null
            ? repository.Diff.Compare<TreeChanges>(baseCommit.Tree, DiffTargets.WorkingDirectory | DiffTargets.Index)
            : repository.Diff.Compare<TreeChanges>(baseCommit.Tree, headCommit.Tree);
        foreach (var change in changes)
        {
            yield return Path.GetFullPath(change.Path, repository.Info.WorkingDirectory);
        }
    }

    private static OutputFormat GetOutputFormatFromExtension(string extension) => extension switch
    {
        ".slnf" => OutputFormat.Slnf,
        ".proj" => OutputFormat.Traversal,
        ".json" => OutputFormat.Json,
        _ => OutputFormat.Plain
    };


    private sealed class DiffOutput
    {
        private readonly FileInfo? _outputFile;
        private readonly IExtendedConsole _console;

        public DiffOutput(FileInfo? outputFile, IExtendedConsole console)
        {
            _outputFile = outputFile;
            _console = console;
        }

        public string RootDirectory => _outputFile?.DirectoryName ?? _console.WorkingDirectory;

        public Stream Open()
        {
            return _outputFile?.Create() ?? _console.OpenStandardOutput();
        }
    }
}