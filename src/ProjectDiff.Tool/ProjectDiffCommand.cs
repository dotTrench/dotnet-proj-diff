using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

    private static readonly Argument<FileInfo> SolutionArgument = new("solution")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description = "Path to solution file to derive projects from",
        Validators =
        {
            x =>
            {
                var f = x.GetValueOrDefault<FileInfo?>();
                if (f is null)
                {
                    x.AddError("{x.Argument.Name} must be specified");
                }
                else if (!f.Exists)
                {
                    x.AddError($"File '{f.FullName}' does not exist.");
                }
                else if (f.Extension is not (".sln" or ".slnx"))
                {
                    x.AddError($"File '{f.FullName}' is not a valid sln file.");
                }
            }
        }
    };

    private static readonly Option<string> BaseCommitOption = new(
        "--base-ref",
        "--base"
    )
    {
        Description = "Base git reference to compare against, if not specified 'HEAD' will be used",
        DefaultValueFactory = _ => "HEAD",
        Required = true
    };

    private static readonly Option<string?> HeadCommitOption = new("--head-ref", "--head")

    {
        Description = "Head git reference to compare against. If not specified current working tree will be used",
        Required = false
    };

    private static readonly Option<bool> MergeBaseOption = new("--merge-base")
    {
        Description =
            "If true instead of using --base use the merge base of --base and --head as the --base reference, if --head is not specified 'HEAD' will be used",
        DefaultValueFactory = _ => true,
    };

    private static readonly Option<bool> IncludeDeleted = new("--include-deleted")
    {
        DefaultValueFactory = _ => false,
        Description = "If true deleted projects will be included in output"
    };

    private static readonly Option<bool> IncludeModified = new("--include-modified")
    {
        DefaultValueFactory = _ => true,
        Description = "If true modified projects will be included in output"
    };

    private static readonly Option<bool> IncludeAdded = new("--include-added")
    {
        DefaultValueFactory = _ => true,
        Description = "If true added projects will be included in output"
    };

    private static readonly Option<bool> IncludeReferencing = new("--include-referencing")
    {
        DefaultValueFactory = _ => true,
        Description = "if true  projects referencing modified/deleted/added projects will be included in output"
    };

    private static readonly Option<OutputFormat?> Format = new("--format", "-f")
    {
        Description =
            "Output format, if --output is specified format will be derived from file extension. Otherwise this defaults to 'plain'"
    };

    private static readonly Option<bool> AbsolutePaths = new("--absolute-paths")
    {
        DefaultValueFactory = _ => false,
        Description =
            "Output absolute paths, if not specified paths will be relative to the working directory. Or relative to --output if specified. This option will not affect slnf format as this requires relative paths"
    };

    private static readonly Option<FileInfo?> OutputOption = new("--output", "--out", "-o")
    {
        Description = "Output file, if not set stdout will be used"
    };

    private static readonly Option<FileInfo[]> IgnoreChangedFilesOption = new("--ignore-changed-file")
    {

        DefaultValueFactory = _ => [],
        Description =
            "Ignore changes in specific files. If these files are a part of the build evaluation process they will still be evaluated, however these files will be considered unchanged by the diff process"
    };

    private readonly IConsole _console;


    public ProjectDiffCommand(IConsole console)
    {
        _console = console;
        Description = "Calculate which projects in a solution has changed since a specific commit";
        Arguments.Add(SolutionArgument);
        Options.Add(BaseCommitOption);
        Options.Add(HeadCommitOption);
        Options.Add(MergeBaseOption);
        Options.Add(IncludeDeleted);
        Options.Add(IncludeModified);
        Options.Add(IncludeAdded);
        Options.Add(IncludeReferencing);
        Options.Add(AbsolutePaths);
        Options.Add(Format);
        Options.Add(OutputOption);
        Options.Add(IgnoreChangedFilesOption);
        SetAction(ExecuteAsync);
    }

    private Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = new ProjectDiffSettings
        {
            Format = parseResult.GetValue(Format),
            Output = parseResult.GetValue(OutputOption),
            Solution = parseResult.GetRequiredValue(SolutionArgument),
            BaseRef = parseResult.GetRequiredValue(BaseCommitOption),
            HeadRef = parseResult.GetValue(HeadCommitOption),
            MergeBase = parseResult.GetValue(MergeBaseOption),
            IncludeDeleted = parseResult.GetValue(IncludeDeleted),
            IncludeModified = parseResult.GetValue(IncludeModified),
            IncludeAdded = parseResult.GetValue(IncludeAdded),
            IncludeReferencing = parseResult.GetValue(IncludeReferencing),
            AbsolutePaths = parseResult.GetValue(AbsolutePaths),
            IgnoreChangedFile = parseResult.GetRequiredValue(IgnoreChangedFilesOption)
        };

        return ExecuteCoreAsync(settings, cancellationToken);
    }

    private async Task<int> ExecuteCoreAsync(
        ProjectDiffSettings settings,
        CancellationToken cancellationToken
    )
    {
        var diffOutput = new DiffOutput(settings.Output, _console);
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
                FindMergeBase = settings.MergeBase,
                IgnoreChangedFiles = settings.IgnoreChangedFile,
            }
        );

        var result = await executor.GetProjectDiff(
            settings.Solution,
            settings.BaseRef,
            settings.HeadRef,
            cancellationToken
        );

        if (result.Status != ProjectDiffExecutionStatus.Success)
        {
            _console.Error.WriteLine(result.Status.ToString());
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
                WriteError(_console, $"Unknown output format {settings.Format}");
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
        diff = diff.Select(project => project with
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
            ).Replace('/', '\\');
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

    private static OutputFormat GetOutputFormatFromExtension(string extension) => extension switch
    {
        ".slnf" => OutputFormat.Slnf,
        ".proj" => OutputFormat.Traversal,
        ".json" => OutputFormat.Json,
        _ => OutputFormat.Plain
    };


    private sealed class DiffOutput(FileInfo? outputFile, IConsole console)
    {
        public string RootDirectory => outputFile?.DirectoryName ?? console.WorkingDirectory;

        public Stream Open()
        {
            return outputFile?.Create() ?? console.OpenStandardOutput();
        }
    }
}