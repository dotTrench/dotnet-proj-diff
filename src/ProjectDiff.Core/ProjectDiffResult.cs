namespace ProjectDiff.Core;

public sealed record ProjectDiffResult
{
    public required ProjectDiffExecutionStatus Status { get; init; }
    public IEnumerable<DiffProject> Projects { get; init; } = [];
    public IReadOnlyCollection<string> ChangedFiles { get; init; } = [];

    public bool IsSuccessful => Status == ProjectDiffExecutionStatus.Success;
}
