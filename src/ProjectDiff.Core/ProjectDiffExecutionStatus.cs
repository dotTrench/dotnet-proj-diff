namespace ProjectDiff.Core;

public enum ProjectDiffExecutionStatus
{
    Success,

    BaseCommitNotFound,
    HeadCommitNotFound,
    MergeBaseNotFound,
}
