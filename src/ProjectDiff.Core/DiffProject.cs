namespace ProjectDiff.Core;

public sealed record DiffProject
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    public required DiffStatus Status { get; init; }

    public required IReadOnlyCollection<string> ReferencedProjects { get; init; } = [];
}
