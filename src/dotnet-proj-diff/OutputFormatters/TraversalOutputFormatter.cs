using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using ProjectDiff.Core;

namespace ProjectDiff.Tool.OutputFormatters;

public sealed class TraversalOutputFormatter : IOutputFormatter
{
    private readonly string? _microsoftBuildTraversalVersion;
    private readonly bool _absolutePaths;

    public TraversalOutputFormatter(string? microsoftBuildTraversalVersion, bool absolutePaths)
    {
        _microsoftBuildTraversalVersion = microsoftBuildTraversalVersion;
        _absolutePaths = absolutePaths;
    }

    public async Task WriteAsync(
        IEnumerable<DiffProject> projects,
        Output output,
        CancellationToken cancellationToken = default
    )
    {
        using var projectCollection = new ProjectCollection();
        var element = ProjectRootElement.Create(projectCollection, NewProjectFileOptions.None);
        element.Sdk = _microsoftBuildTraversalVersion != null
            ? $"Microsoft.Build.Traversal/{_microsoftBuildTraversalVersion}"
            : "Microsoft.Build.Traversal";

        foreach (var project in projects)
        {
            var path = !_absolutePaths
                ? Path.GetRelativePath(output.RootDirectory, project.Path).Replace('/', '\\')
                : project.Path.Replace('/', '\\');

            element.AddItem("ProjectReference", path);
        }

        await using var stream = output.Open();
        await using var writer = new StreamWriter(stream);
        element.Save(writer);
    }
}
