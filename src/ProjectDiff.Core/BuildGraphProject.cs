namespace ProjectDiff.Core;

public sealed class BuildGraphProject
{
    public BuildGraphProject(
        string fullPath,
        IReadOnlyCollection<string> inputFiles,
        IReadOnlyCollection<string> references,
        IReadOnlyCollection<ProjectPackageReference> packageReferences
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
    public IReadOnlyCollection<ProjectPackageReference> PackageReferences { get; }

    public bool Matches(BuildGraphProject other)
    {
        return FullPath == other.FullPath;
    }
}