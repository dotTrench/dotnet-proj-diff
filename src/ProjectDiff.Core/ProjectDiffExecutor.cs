using System.Collections.Frozen;
using System.IO.Enumeration;
using LibGit2Sharp;

namespace ProjectDiff.Core;

public sealed class ProjectDiffExecutorOptions
{
    public bool FindMergeBase { get; init; }
    public string[] IgnoredFilePatterns { get; init; } = [];
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

        var changedFiles = GetGitModifiedFiles(repo, baseCommit, null)
            .Where(ShouldIncludeFile)
            .ToFrozenSet();

        if (changedFiles.Count == 0)
        {
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.Success,
                ChangedFiles = [],
                Projects = []
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

        var toBuildGraph = BuildGraphFactory.CreateForProjectGraph(toGraph, changedFiles);
        var fromBuildGraph = BuildGraphFactory.CreateForProjectGraph(fromGraph, changedFiles);

        return new ProjectDiffResult
        {
            Status = ProjectDiffExecutionStatus.Success,
            ChangedFiles = changedFiles,
            Projects = BuildGraphDiff.Diff(fromBuildGraph, toBuildGraph, changedFiles),
        };

        bool ShouldIncludeFile(string file)
        {
            if (_options.IgnoredFilePatterns.Length == 0)
            {
                return true;
            }

            return !_options.IgnoredFilePatterns.Any(
                pattern => FileSystemName.MatchesSimpleExpression(pattern, file, false)
            );
        }
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