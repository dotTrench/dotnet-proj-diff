using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tool.OutputFormatters;

namespace ProjectDiff.Tool;

public sealed class ProjectDiffCommand : RootCommand
{
    public static readonly JsonSerializerOptions JsonSerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter<DiffStatus>()
            }
        };

    private static readonly Option<FileInfo> SolutionOption = new("--solution")
    {
        Arity = ArgumentArity.ZeroOrOne,
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

    private static readonly Option<LogLevel> LogLevelOption = new("--log-level")
    {
        DefaultValueFactory = _ => LogLevel.Information,
        Description = "Set the log level for the command",
    };

    private static readonly Option<string?> MicrosoftBuildTraversalVersionOption = new("--msbuild-traversal-version")
    {
        Description = "Set the version of the Microsoft.Build.Traversal SDK when using traversal output format",
        DefaultValueFactory = _ => null,
    };

    private readonly IConsole _console;


    public ProjectDiffCommand(IConsole console)
    {
        _console = console;
        Description = "Calculate which projects in a solution has changed since a specific commit";
        Options.Add(SolutionOption);
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
        Options.Add(LogLevelOption);
        Options.Add(MicrosoftBuildTraversalVersionOption);
        SetAction(ExecuteAsync);
    }

    private Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var settings = new ProjectDiffSettings
        {
            Format = parseResult.GetValue(Format),
            Output = parseResult.GetValue(OutputOption),
            Solution = parseResult.GetValue(SolutionOption),
            BaseRef = parseResult.GetRequiredValue(BaseCommitOption),
            HeadRef = parseResult.GetValue(HeadCommitOption),
            MergeBase = parseResult.GetValue(MergeBaseOption),
            IncludeDeleted = parseResult.GetValue(IncludeDeleted),
            IncludeModified = parseResult.GetValue(IncludeModified),
            IncludeAdded = parseResult.GetValue(IncludeAdded),
            IncludeReferencing = parseResult.GetValue(IncludeReferencing),
            AbsolutePaths = parseResult.GetValue(AbsolutePaths),
            IgnoreChangedFile = parseResult.GetRequiredValue(IgnoreChangedFilesOption),
            LogLevel = parseResult.GetValue(LogLevelOption),
            MicrosoftBuildTraversalVersion = parseResult.GetValue(MicrosoftBuildTraversalVersionOption)
        };

        return ExecuteCoreAsync(settings, cancellationToken);
    }

    private async Task<int> ExecuteCoreAsync(
        ProjectDiffSettings settings,
        CancellationToken cancellationToken
    )
    {
        using var loggerFactory = LoggerFactory.Create(x =>
            {
                x.AddConsole(c => c.LogToStandardErrorThreshold = LogLevel.Trace); // Log everything to stderr
                x.SetMinimumLevel(settings.LogLevel);
            }
        );
        var logger = loggerFactory.CreateLogger<ProjectDiffCommand>();

        OutputFormat outputFormat;
        if (settings.Format is not null)
        {
            outputFormat = settings.Format.Value;
        }
        else if (settings.Output is not null)
        {
            logger.LogDebug("Detecting output format from file extension {Extension}", settings.Output.Extension);
            outputFormat = GetOutputFormatFromExtension(settings.Output.Extension);
            logger.LogDebug("Detected output format {Format}", outputFormat);
        }
        else
        {
            outputFormat = OutputFormat.Plain;
        }

        if (outputFormat == OutputFormat.Slnf && settings.Solution is null)
        {
            logger.LogError("Cannot output as slnf format without {SolutionOption} specified.", SolutionOption.Name);
            return 1;
        }

        logger.LogDebug("Using output format {Format}", outputFormat);

        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                FindMergeBase = settings.MergeBase,
                IgnoreChangedFiles = settings.IgnoreChangedFile,
            },
            loggerFactory
        );

        IEntrypointProvider entrypointProvider = settings.Solution is not null
            ? new SolutionEntrypointProvider(
                settings.Solution,
                loggerFactory.CreateLogger<SolutionEntrypointProvider>()
            )
            : new DirectoryScanEntrypointProvider(
                loggerFactory.CreateLogger<DirectoryScanEntrypointProvider>()
            );

        var result = await executor.GetProjectDiff(
            settings.Solution?.DirectoryName ?? _console.WorkingDirectory,
            entrypointProvider,
            settings.BaseRef,
            settings.HeadRef,
            cancellationToken
        );

        if (result.Status != ProjectDiffExecutionStatus.Success)
        {
            logger.LogError("Failed to calculate project diff '{Status}'", result.Status);
            return 1;
        }

        var projects = result.Projects
            .Where(ShouldInclude)
            .OrderBy(it => it.ReferencedProjects.Count)
            .ThenBy(it => it.Path)
            .ToList();

        logger.LogInformation("Found {Count} projects in diff", projects.Count);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Diff projects: {Projects}",
                projects.Select(it => new
                    { it.Path, it.Status, ReferencedProjects = string.Join(',', it.ReferencedProjects) }
                )
            );
        }

        var formatter = GetFormatter(outputFormat, settings);
        logger.LogDebug("Using output formatter {Formatter}", formatter.GetType().Name);

        var output = new Output(settings.Output, _console);
        await formatter.WriteAsync(
            projects,
            output,
            cancellationToken
        );

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

    private static IOutputFormatter GetFormatter(OutputFormat format, ProjectDiffSettings settings) => format switch
    {
        OutputFormat.Json => new JsonOutputFormatter(settings.AbsolutePaths, JsonSerializerOptions),
        OutputFormat.Plain => new PlainOutputFormatter(settings.AbsolutePaths),
        OutputFormat.Slnf => new SlnfOutputFormatter(
            settings.Solution ?? throw new ArgumentException("Solution must be set when using SlnfOutputFormatter"),
            JsonSerializerOptions
        ),
        OutputFormat.Traversal => new TraversalOutputFormatter(
            settings.MicrosoftBuildTraversalVersion,
            settings.AbsolutePaths
        ),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown output format")
    };

    private static OutputFormat GetOutputFormatFromExtension(string extension) => extension switch
    {
        ".slnf" => OutputFormat.Slnf,
        ".proj" => OutputFormat.Traversal,
        ".json" => OutputFormat.Json,
        _ => OutputFormat.Plain
    };
}