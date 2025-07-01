using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class MultiFrameworkTests
{
    [Fact]
    public static async Task FileModifiedInMultiFrameworkProjectOnlyReturnsASingleProject()
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var sln = await r.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
                r.CreateDirectory("Sample");
                var project = r.CreateProject(
                    "Sample/Sample.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net9.0;net8.0;netstandard2.0"); }
                );
                await r.WriteFileAsync("Sample/MyClass.cs", "// Some content");

                return (sln, project);
            }
        );
        var ((sln, project), repo) = res;
        await repo.WriteFileAsync("Sample/MyClass.cs", "// Some new content");
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                FindMergeBase = false
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "HEAD",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var proj = Assert.Single(result.Projects);

        Assert.Equal(DiffStatus.Modified, proj.Status);
        Assert.Equal(project, proj.Path);
    }
}