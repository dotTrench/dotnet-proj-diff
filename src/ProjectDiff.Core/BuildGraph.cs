namespace ProjectDiff.Core;

public sealed class BuildGraph
{
    public required IReadOnlyCollection<BuildGraphProject> Projects { get; init; }
    
}