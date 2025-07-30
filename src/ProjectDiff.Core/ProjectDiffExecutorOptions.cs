namespace ProjectDiff.Core;

public sealed class ProjectDiffExecutorOptions
{
    public bool FindMergeBase { get; init; }
    public FileInfo[] IgnoreChangedFiles { get; init; } = [];
    public IReadOnlyDictionary<string, string> GlobalProperties { get; init; } = new Dictionary<string, string>(0);
}
