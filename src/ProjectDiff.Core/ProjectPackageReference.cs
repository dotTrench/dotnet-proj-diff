namespace ProjectDiff.Core;

public sealed record ProjectPackageReference
{
    public required string Name { get; init; }
    public required string Version { get; init; }
}