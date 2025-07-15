using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;
using ProjectDiff.Tool;
using ProjectDiff.Tool.OutputFormatters;

namespace ProjectDiff.Tests.Tool.OutputFormatters;

public sealed class PlainOutputFormatterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_OutputsPlainText(bool absolutePaths)
    {
        var directory = Path.GetTempPath();
        // Arrange
        var projects = new List<DiffProject>
        {
            new()
            {
                Path = Path.Combine(directory, "ProjectA"),
                Status = DiffStatus.Added,
                ReferencedProjects = [],
            },
            new()
            {
                Path = Path.Combine(directory, "ProjectB"),
                Status = DiffStatus.ReferenceChanged,
                ReferencedProjects = [],
            }
        };

        var console = new TestConsole(directory);
        var output = new Output(null, console);
        var formatter = new PlainOutputFormatter(absolutePaths);

        // Act
        await formatter.WriteAsync(projects, output, TestContext.Current.CancellationToken);

        await Verify(console.GetStandardOutput())
            .UseParameters(absolutePaths);
    }
}