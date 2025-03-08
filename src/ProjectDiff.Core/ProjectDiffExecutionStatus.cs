namespace ProjectDiff.Core;

public enum ProjectDiffExecutionStatus
{
    Success,

    RepositoryNotFound,
    CommitNotFound,
    MergeBaseNotFound,
}