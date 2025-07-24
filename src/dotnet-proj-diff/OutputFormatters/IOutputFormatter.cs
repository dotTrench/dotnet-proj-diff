using ProjectDiff.Core;

namespace ProjectDiff.Tool.OutputFormatters;

public interface IOutputFormatter
{
    Task WriteAsync(
        IEnumerable<DiffProject> projects,
        Output output,
        CancellationToken cancellationToken = default
    );
}
