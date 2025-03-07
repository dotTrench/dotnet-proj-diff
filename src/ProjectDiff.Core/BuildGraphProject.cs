namespace ProjectDiff.Core;

public sealed class BuildGraphProject
{
    public BuildGraphProject(
        string fullPath,
        IReadOnlyCollection<string> inputFiles,
        IReadOnlyCollection<string> references,
        IReadOnlyCollection<BuildGraphProjectPackageReference> packageReferences
    )
    {
        FullPath = fullPath;
        InputFiles = inputFiles;
        References = references;
        PackageReferences = packageReferences;
    }

    public string FullPath { get; }
    public IReadOnlyCollection<string> InputFiles { get; }
    public IReadOnlyCollection<string> References { get; }
    public IReadOnlyCollection<BuildGraphProjectPackageReference> PackageReferences { get; }

    public bool Matches(BuildGraphProject other)
    {
        return FullPath == other.FullPath;
    }
}

public sealed record BuildGraphProjectPackageReference
{
    public required string Name { get; init; }
    public required string Version { get; init; }
}