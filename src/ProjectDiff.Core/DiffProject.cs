namespace ProjectDiff.Core;

public sealed record DiffProject
{
    public required string Path { get; init; }
    public required DiffStatus Status { get; init; }
}