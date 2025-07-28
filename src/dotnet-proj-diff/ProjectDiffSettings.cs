using Microsoft.Extensions.Logging;

namespace ProjectDiff.Tool;

public sealed class ProjectDiffSettings
{
    public required string BaseRef { get; init; }
    public required string? HeadRef { get; init; }
    public required FileInfo? Solution { get; init; }
    public required bool MergeBase { get; init; } = true;
    public required bool IncludeDeleted { get; init; }
    public required bool IncludeModified { get; init; }
    public required bool IncludeAdded { get; init; }
    public required bool IncludeReferencing { get; init; }
    public required bool AbsolutePaths { get; init; }
    public required OutputFormat? Format { get; init; }
    public required FileInfo? Output { get; init; }
    public required FileInfo[] IgnoreChangedFile { get; init; } = [];
    public string? MicrosoftBuildTraversalVersion { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public required string[] ExcludeProjects { get; init; }
    public required string[] IncludeProjects { get; init; }
}
