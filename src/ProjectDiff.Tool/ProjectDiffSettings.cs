namespace ProjectDiff.Tool;

public sealed class ProjectDiffSettings
{
    public required string Commit { get; init; }
    public required FileInfo Solution { get; init; }
    public bool MergeBase { get; init; } = true;
    public bool IncludeDeleted { get; init; }
    public bool IncludeModified { get; init; }
    public bool IncludeAdded { get; init; }
    public bool IncludeReferencing { get; init; }
    public bool AbsolutePaths { get; init; }
    public OutputFormat? Format { get; init; }
    public FileInfo? Output { get; init; }
    public FileInfo[] IgnoreChangedFile { get; init; } = [];
}