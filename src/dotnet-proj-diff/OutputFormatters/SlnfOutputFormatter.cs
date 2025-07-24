using System.Text.Json;
using System.Text.Json.Nodes;
using ProjectDiff.Core;

namespace ProjectDiff.Tool.OutputFormatters;

public class SlnfOutputFormatter : IOutputFormatter
{
    private readonly FileInfo _solution;
    private readonly JsonSerializerOptions _options;

    public SlnfOutputFormatter(FileInfo solution, JsonSerializerOptions options)
    {
        _solution = solution;
        _options = options;
    }

    public async Task WriteAsync(
        IEnumerable<DiffProject> projects,
        Output output,
        CancellationToken cancellationToken = default
    )
    {
        var solutionObject = new JsonObject
        {
            {
                "path", Path.GetRelativePath(
                    output.RootDirectory,
                    _solution.FullName
                )
            }
        };
        var projectArray = new JsonArray();
        foreach (var project in projects)
        {
            var projectPath = Path.GetRelativePath(
                _solution.DirectoryName!,
                project.Path
            ).Replace('/', '\\');
            projectArray.Add(projectPath);
        }

        solutionObject.Add("projects", projectArray);

        var root = new JsonObject { { "solution", solutionObject } };

        await using var stream = output.Open();
        await JsonSerializer.SerializeAsync(stream, root, _options, cancellationToken);
    }
}
