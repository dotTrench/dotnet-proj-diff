using LibGit2Sharp;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core.Entrypoints;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ProjectDiff.Core;

public class ProjectDiffExecutor
{
    private readonly ProjectDiffExecutorOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ProjectDiffExecutor> _logger;

    public ProjectDiffExecutor(ProjectDiffExecutorOptions options, ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<ProjectDiffExecutor>();
    }

    public async Task<ProjectDiffResult> GetProjectDiff(
        Repository repository,
        IProjectGraphEntryPointProvider projectGraphEntryPointProvider,
        string baseCommitRef = "HEAD",
        string? headCommitRef = null,
        CancellationToken cancellationToken = default
    )
    {
        if (repository.Info.IsShallow)
        {
            _logger.LogWarning("Repository is shallow, some operations may not work as expected");
        }

        _logger.LogDebug("Looking up base commit '{BaseCommitRef}'", baseCommitRef);
        var baseCommit = repository.Lookup<Commit>(baseCommitRef);
        if (baseCommit is null)
        {
            _logger.LogError("Base commit '{BaseCommitRef}' not found in repository", baseCommitRef);
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.BaseCommitNotFound,
            };
        }

        Commit? headCommit;
        if (headCommitRef is not null)
        {
            _logger.LogDebug("Looking up head commit '{HeadCommitRef}'", headCommitRef);
            headCommit = repository.Lookup<Commit>(headCommitRef);

            if (headCommit is null)
            {
                _logger.LogError("Head commit '{HeadCommitRef}' not found in repository", headCommitRef);
                return new ProjectDiffResult
                {
                    Status = ProjectDiffExecutionStatus.HeadCommitNotFound
                };
            }
        }
        else
        {
            _logger.LogDebug("No head commit specified, using working directory state");
            headCommit = null;
        }

        if (_options.FindMergeBase)
        {
            _logger.LogDebug(
                "Finding merge base between base commit '{BaseCommitRef}' and head commit '{HeadCommitRef}'",
                baseCommitRef,
                headCommitRef
            );
            var head = headCommit ?? repository.Head.Tip;
            var mergeBaseCommit = repository.ObjectDatabase.FindMergeBase(baseCommit, head);
            if (mergeBaseCommit is null)
            {
                _logger.LogError(
                    "Could not find merge base between base commit '{BaseCommitRef}' and head commit '{HeadCommitRef}'",
                    baseCommitRef,
                    head.Sha
                );
                return new ProjectDiffResult
                {
                    Status = ProjectDiffExecutionStatus.MergeBaseNotFound
                };
            }

            _logger.LogDebug(
                "Found merge base commit '{MergeBaseCommit}'",
                mergeBaseCommit.Sha
            );

            baseCommit = mergeBaseCommit;
        }

        _logger.LogInformation(
            "Finding changed files between commits '{BaseCommitRef}' and '{HeadCommitRef}'",
            baseCommitRef,
            headCommitRef ?? "working directory"
        );
        var changedFiles = GetGitModifiedFiles(repository, baseCommit, headCommit)
            .Where(ShouldIncludeFile)
            .ToList();

        if (changedFiles.Count == 0)
        {
            _logger.LogInformation(
                "No changed files found between commits '{BaseCommitRef}' and '{HeadCommitRef}'",
                baseCommitRef,
                headCommitRef ?? "working directory"
            );
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.Success,
                ChangedFiles = [],
                Projects = []
            };
        }

        _logger.LogInformation("Found {NumChangedFiles} changed files", changedFiles.Count);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Found changed files: {ChangedFiles}", changedFiles);
        }

        var projectGraphFactory = new ProjectGraphFactory(_loggerFactory);

        BuildGraph baseBuildGraph;
        using (_logger.BeginScope("Building base graph"))
        {
            baseBuildGraph = await CreateBuildGraph(
                repository,
                projectGraphFactory,
                projectGraphEntryPointProvider,
                baseCommit,
                cancellationToken
            );
        }

        BuildGraph headBuildGraph;
        using (_logger.BeginScope("Building head graph"))
        {
            headBuildGraph = await CreateBuildGraph(
                repository,
                projectGraphFactory,
                projectGraphEntryPointProvider,
                headCommit,
                cancellationToken
            );
        }


        var projects = BuildGraphDiff.Diff(baseBuildGraph, headBuildGraph, changedFiles, _loggerFactory);
        return new ProjectDiffResult
        {
            Status = ProjectDiffExecutionStatus.Success,
            ChangedFiles = changedFiles,
            Projects = projects,
        };

        bool ShouldIncludeFile(string file) =>
            _options.IgnoreChangedFiles.Length == 0 || _options.IgnoreChangedFiles.All(it => it.FullName != file);
    }

    private async Task<BuildGraph> CreateBuildGraph(
        Repository repository,
        ProjectGraphFactory projectGraphFactory,
        IProjectGraphEntryPointProvider projectGraphEntryPointProvider,
        Commit? headCommit,
        CancellationToken cancellationToken
    )
    {
        ProjectGraph projectGraph;
        if (headCommit is null)
        {
            projectGraph = await projectGraphFactory.BuildForWorkingDirectory(
                projectGraphEntryPointProvider,
                cancellationToken
            );
        }
        else
        {
            projectGraph = await projectGraphFactory.BuildForGitTree(
                repository,
                headCommit.Tree,
                projectGraphEntryPointProvider,
                cancellationToken
            );
        }

        return BuildGraphFactory.CreateForProjectGraph(
            projectGraph,
            repository,
            _options.IgnoreChangedFiles
        );
    }

    private static IEnumerable<string> GetGitModifiedFiles(
        Repository repository,
        Commit baseCommit,
        Commit? headCommit
    )
    {
        using var changes = headCommit is null
            ? repository.Diff.Compare<TreeChanges>(
                baseCommit.Tree,
                DiffTargets.WorkingDirectory | DiffTargets.Index
            )
            : repository.Diff.Compare<TreeChanges>(baseCommit.Tree, headCommit.Tree);
        foreach (var change in changes)
        {
            yield return Path.GetFullPath(change.Path, repository.Info.WorkingDirectory);
        }
    }
}
