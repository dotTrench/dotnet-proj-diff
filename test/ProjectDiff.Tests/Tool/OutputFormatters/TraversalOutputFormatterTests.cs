using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;
using ProjectDiff.Tool;
using ProjectDiff.Tool.OutputFormatters;

namespace ProjectDiff.Tests.Tool.OutputFormatters;

public sealed class TraversalOutputFormatterTests
{
    public static TheoryData<string?, bool> TraversalOutputFormatterTestData() => new MatrixTheoryData<string?, bool>(
        ["1.0", "4.0", null],
        [true, false]
    );

    [Theory]
    [MemberData(nameof(TraversalOutputFormatterTestData))]
    public async Task WriteAsync_ShouldWriteTraversalOutput(string? traversalVersion, bool absolutePaths)
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
                ReferencedProjects =
                [
                    Path.Combine(directory, "ProjectA")
                ],
            }
        };

        var console = new TestConsole(directory);
        var output = new Output(null, console);
        var formatter = new TraversalOutputFormatter(traversalVersion, absolutePaths);

        // Act
        await formatter.WriteAsync(projects, output);


        await VerifyXml(console.GetStandardOutput())
            .UseParameters(traversalVersion, absolutePaths);
    }
}