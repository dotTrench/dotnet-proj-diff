namespace ProjectDiff.Core;

public sealed class ProjectDiffExecutorOptions
{
    public bool FindMergeBase { get; init; }
    public FileInfo[] IgnoreChangedFiles { get; init; } = [];
}
