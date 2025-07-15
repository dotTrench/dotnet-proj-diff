using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;
using ProjectDiff.Tool;
using ProjectDiff.Tool.OutputFormatters;

namespace ProjectDiff.Tests.Tool.OutputFormatters;

public sealed class JsonOutputFormatterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_OutputsJson(bool absolutePaths)
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
                ReferencedProjects = [
                    Path.Combine(directory, "ProjectA")
                ],
            }
        };
        var console = new TestConsole(directory);
        var output = new Output(null, console);
        var formatter = new JsonOutputFormatter(absolutePaths, ProjectDiffCommand.JsonSerializerOptions);

        // Act
        await formatter.WriteAsync(projects, output, TestContext.Current.CancellationToken);

        await VerifyJson(console.GetStandardOutput())
            .UseParameters(absolutePaths);
    }
}