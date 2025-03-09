using System.Collections.Frozen;
using LibGit2Sharp;
using Microsoft.Build.Graph;

namespace ProjectDiff.Core;

public class ProjectDiffExecutor
{
    private readonly ProjectDiffExecutorOptions _options;

    public ProjectDiffExecutor(ProjectDiffExecutorOptions options)
    {
        _options = options;
    }

    public async Task<ProjectDiffResult> GetProjectDiff(
        FileInfo solutionFile,
        string baseCommitRef = "HEAD",
        string? headCommitRef = null,
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
        var baseCommit = repo.Lookup<Commit>(baseCommitRef);
        if (baseCommit is null)
        {
            return new ProjectDiffResult
            {
                Status = ProjectDiffExecutionStatus.BaseCommitNotFound,
            };
        }

        Commit? headCommit;
        if (headCommitRef is not null)
        {
            headCommit = repo.Lookup<Commit>(headCommitRef);

            if (headCommit is null)
            {
                return new ProjectDiffResult
                {
                    Status = ProjectDiffExecutionStatus.HeadCommitNotFound
                };
            }
        }
        else
        {
            headCommit = null;
        }

        if (_options.FindMergeBase)
        {
            var mergeBaseCommit = repo.ObjectDatabase.FindMergeBase(baseCommit, headCommit ?? repo.Head.Tip);
            if (mergeBaseCommit is null)
            {
                return new ProjectDiffResult
                {
                    Status = ProjectDiffExecutionStatus.MergeBaseNotFound
                };
            }

            baseCommit = mergeBaseCommit;
        }

        var changedFiles = GetGitModifiedFiles(repo, baseCommit, headCommit)
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

        var fromGraph = await ProjectGraphFactory.BuildForGitTree(
            repo,
            baseCommit.Tree,
            solutionFile,
            cancellationToken
        );

        ProjectGraph toGraph;
        if (headCommit is null)
        {
            toGraph = await ProjectGraphFactory.BuildForWorkingDirectory(
                solutionFile,
                cancellationToken
            );
        }
        else
        {
            toGraph = await ProjectGraphFactory.BuildForGitTree(
                repo,
                headCommit.Tree,
                solutionFile,
                cancellationToken
            );
        }


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
            if (_options.IgnoreChangedFiles.Length == 0)
            {
                return true;
            }

            return _options.IgnoreChangedFiles.All(it => it.FullName != file);
        }
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