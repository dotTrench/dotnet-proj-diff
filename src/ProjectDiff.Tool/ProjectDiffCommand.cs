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
        "Output absolute paths, if not specified paths will be relative to the working directory. Or relative to --output if specified. This option will not affect slnf format as this requires relative paths"
    );

    private readonly Option<FileInfo?> _outputOption = new(
        "--output",
        "Output file, if not set stdout will be used"
    );

    private readonly Option<bool> _checkPackageReferences = new(
        "--check-package-references",
        () => false,
        "Resolve PackageReference versions for each project and compare versions. This disables diff checking of Directory.Packages.props files. You probably want to enable this if you're using CentralPackageManagement(CPM) in NuGet"
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
        AddOption(_checkPackageReferences);
        Handler = CommandHandler.Create(ExecuteAsync);
    }


    private async Task<int> ExecuteAsync(
        ProjectDiffSettings settings,
        IConsole c,
        CancellationToken cancellationToken
    )
    {
        if (c is not IExtendedConsole console)
        {
            throw new InvalidOperationException("Expected IExtendedConsole");
        }

        var diffOutput = new DiffOutput(settings.Output, console);
        OutputFormat outputFormat;
        if (settings.Format is not null)
        {
            outputFormat = settings.Format.Value;
        }
        else if (settings.Output is not null)
        {
            outputFormat = GetOutputFormatFromExtension(settings.Output.Extension);
        }
        else
        {
            outputFormat = OutputFormat.Plain;
        }

        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                CheckPackageReferences = settings.CheckPackageReferences,
                FindMergeBase = settings.MergeBase
            }
        );

        var result = await executor.GetProjectDiff(settings.Solution, settings.Commit, cancellationToken);

        if (result.Status != ProjectDiffExecutionStatus.Success)
        {
            console.Error.WriteLine(result.Status.ToString());
            return 1;
        }

        var diff = result.Projects.Where(ShouldInclude);
        switch (outputFormat)
        {
            case OutputFormat.Plain:
                await WritePlain(diffOutput, diff, settings.AbsolutePaths);
                break;
            case OutputFormat.Json:
                await WriteJson(diffOutput, diff, settings.AbsolutePaths);
                break;
            case OutputFormat.Slnf:
                await WriteSlnf(diffOutput, settings.Solution, diff);
                break;
            case OutputFormat.Traversal:
                await WriteTraversal(diffOutput, diff, settings.AbsolutePaths);
                break;
            default:
                WriteError(console, $"Unknown output format {settings.Format}");
                return 1;
        }

        return 0;

        bool ShouldInclude(DiffProject project) =>
            project.Status switch
            {
                DiffStatus.Removed when settings.IncludeDeleted => true,
                DiffStatus.Added when settings.IncludeAdded => true,
                DiffStatus.Modified when settings.IncludeModified => true,
                DiffStatus.ReferenceChanged when settings.IncludeReferencing => true,
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
            ).Replace('\\', '/');
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