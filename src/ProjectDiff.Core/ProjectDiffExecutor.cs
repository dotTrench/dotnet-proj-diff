using System.Collections.Frozen;
using LibGit2Sharp;

namespace ProjectDiff.Core;

public sealed record ProjectDiffResult
{
    public required ProjectDiffExecutionStatus Status { get; set; }
    public IEnumerable<DiffProject> Projects { get; set; } = [];
}

public enum ProjectDiffExecutionStatus
{
    Success,

    RepositoryNotFound,
    CommitNotFound,
    MergeBaseNotFound,
    NoChangedFiles,
}

public sealed class ProjectDiffExecutorOptions
{
    public bool FindMergeBase { get; init; }
    public bool CheckPackageReferences { get; init; }
}

public class ProjectDiffExecutor
{
    private readonly ProjectDiffExecutorOptions _options;

    public ProjectDiffExecutor(ProjectDiffExecutorOptions options)
    {
        _options = options;
    }

    public async Task<ProjectDiffResult> GetProjectDiff(
        FileInfo solutionFile,
        string commitRef,
        CancellationToken cancellationToken = default
    )
    {
        var solutionDirectory = solutionFile.Directory ?? throw new ArgumentException(
            "solutionFile.Directory is null",
            nameof(solutionFile)
        );
        var repoPath = Repository.Discover(solutionDirectory.FullName);
        if (repoPath is null)
        {
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.RepositoryNotFound
            };
        }

        using var repo = new Repository(repoPath);
        var baseCommit = repo.Lookup<Commit>(commitRef);
        if (baseCommit is null)
        {
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.CommitNotFound,
            };
        }

        if (_options.FindMergeBase)
        {
            var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(baseCommit, repo.Head.Tip);
            if (mergeBaseCommit is null)
            {
                return new ProjectDiffResult
                {
                    Status = ProjectDiffExecutionStatus.MergeBaseNotFound
                };
            }

            baseCommit = mergeBaseCommit;
        }

        var changes = GetGitModifiedFiles(repo, baseCommit, null).ToFrozenSet();
        if (changes.Count == 0)
        {
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.NoChangedFiles
            };
        }


        var toGraph = await ProjectGraphFactory.BuildForWorkingDirectory(
            solutionFile,
            cancellationToken
        );

        var fromGraph = await ProjectGraphFactory.BuildForGitTree(
            repo,
            baseCommit.Tree,
            solutionFile,
            cancellationToken
        );

        var toBuildGraph = BuildGraphFactory.CreateForProjectGraph(toGraph, changes, _options.CheckPackageReferences);
        var fromBuildGraph = BuildGraphFactory.CreateForProjectGraph(fromGraph, changes, _options.CheckPackageReferences);

        return new ProjectDiffResult
        {
            Status = ProjectDiffExecutionStatus.Success,
            Projects = BuildGraphDiff.Diff(fromBuildGraph, toBuildGraph, changes),
        };
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
}