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
        string repositoryPath,
        IEntrypointProvider entrypointProvider,
        string baseCommitRef = "HEAD",
        string? headCommitRef = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Discovering repository from path '{Path}'", repositoryPath);
        var repoPath = Repository.Discover(repositoryPath);
        if (repoPath is null)
        {
            _logger.LogError("Could not find a Git repository for path '{Path}'", repositoryPath);
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.RepositoryNotFound
            };
        }

        _logger.LogDebug("Found repository at '{RepoPath}'", repoPath);

        using var repo = new Repository(repoPath);

        _logger.LogDebug("Looking up base commit '{BaseCommitRef}'", baseCommitRef);
        var baseCommit = repo.Lookup<Commit>(baseCommitRef);
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
            headCommit = repo.Lookup<Commit>(headCommitRef);

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
            var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(baseCommit, headCommit ?? repo.Head.Tip);
            if (mergeBaseCommit is null)
            {
                _logger.LogError(
                    "Could not find merge base between base commit '{BaseCommitRef}' and head commit '{HeadCommitRef}'",
                    baseCommitRef,
                    headCommitRef
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
        var changedFiles = GetGitModifiedFiles(repo, baseCommit, headCommit)
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

        var baseGraph = await projectGraphFactory.BuildForGitTree(
            repo,
            baseCommit.Tree,
            entrypointProvider,
            cancellationToken
        );
        _logger.LogInformation(
            "Base project graph built with {NumProjects} projects",
            baseGraph.ProjectNodes.Count
        );
        _logger.LogDebug("Base project graph construction metrics: {Metrics}", baseGraph.ConstructionMetrics);

        ProjectGraph headGraph;
        if (headCommit is null)
        {
            headGraph = await projectGraphFactory.BuildForWorkingDirectory(
                repo,
                entrypointProvider,
                cancellationToken
            );
        }
        else
        {
            headGraph = await projectGraphFactory.BuildForGitTree(
                repo,
                headCommit.Tree,
                entrypointProvider,
                cancellationToken
            );
        }

        _logger.LogInformation(
            "Head project graph built with {NumProjects} projects",
            headGraph.ProjectNodes.Count
        );
        _logger.LogDebug("Head project graph construction metrics: {Metrics}", headGraph.ConstructionMetrics);


        var headBuildGraph = BuildGraphFactory.CreateForProjectGraph(headGraph, changedFiles);
        var baseBuildGraph = BuildGraphFactory.CreateForProjectGraph(baseGraph, changedFiles);

        return new ProjectDiffResult
        {
            Status = ProjectDiffExecutionStatus.Success,
            ChangedFiles = changedFiles,
            Projects = BuildGraphDiff.Diff(baseBuildGraph, headBuildGraph, changedFiles),
        };

        bool ShouldIncludeFile(string file) =>
            _options.IgnoreChangedFiles.Length == 0 || _options.IgnoreChangedFiles.All(it => it.FullName != file);
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