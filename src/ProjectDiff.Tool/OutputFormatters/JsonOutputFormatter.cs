using System.Text.Json;
using ProjectDiff.Core;

namespace ProjectDiff.Tool.OutputFormatters;

public sealed class JsonOutputFormatter : IOutputFormatter
{
    private readonly bool _absolutePaths;
    private readonly JsonSerializerOptions _options;

    public JsonOutputFormatter(bool absolutePaths, JsonSerializerOptions options)
    {
        _absolutePaths = absolutePaths;
        _options = options;
    }

    public async Task WriteAsync(
        IEnumerable<DiffProject> projects,
        Output output,
        CancellationToken cancellationToken = default
    )
    {
        projects = projects.Select(project => project with
            {
                Path = output.NormalizePath(
                    project.Path,
                    _absolutePaths
                ),
                ReferencedProjects = project.ReferencedProjects
                    .Select(refProject => output.NormalizePath(
                            refProject,
                            _absolutePaths
                        )
                    ).ToList()
            }
        );

        await using var stream = output.Open();
        await JsonSerializer.SerializeAsync(stream, projects, _options, cancellationToken);
    }
}