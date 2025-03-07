namespace ProjectDiff.Tool;

public sealed class ProjectDiffSettings
{
    public string Commit { get; init; } = "HEAD";
    public required FileInfo Solution { get; init; }
    public bool MergeBase { get; init; } = true;
    public bool IncludeDeleted { get; init; } = false;
    public bool IncludeModified { get; init; } = true;
    public bool IncludeAdded { get; init; } = true;
    public bool IncludeReferencing { get; init; } = true;
    public bool AbsolutePaths { get; init; } = false;
    public OutputFormat? Format { get; init; }
    public FileInfo? Output { get; init; }
    public bool CheckPackageReferences { get; init; } = false;
}