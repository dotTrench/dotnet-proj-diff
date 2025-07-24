using ProjectDiff.Core;

namespace ProjectDiff.Tool.OutputFormatters;

public sealed class PlainOutputFormatter : IOutputFormatter
{
    private readonly bool _absolutePaths;

    public PlainOutputFormatter(bool absolutePaths)
    {
        _absolutePaths = absolutePaths;
    }

    public async Task WriteAsync(
        IEnumerable<DiffProject> projects,
        Output output,
        CancellationToken cancellationToken = default
    )
    {
        await using var stream = output.Open();
        await using var writer = new StreamWriter(stream);
        foreach (var project in projects)
        {
            var path = output.NormalizePath(
                project.Path,
                _absolutePaths
            );
            await writer.WriteLineAsync(path);
        }
    }
}
