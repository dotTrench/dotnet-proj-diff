namespace ProjectDiff.Core;

public enum ProjectDiffExecutionStatus
{
    Success,

    RepositoryNotFound,
    BaseCommitNotFound,
    HeadCommitNotFound,
    MergeBaseNotFound,
}
