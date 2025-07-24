using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class MultiFrameworkTests
{
    [Fact]
    public static async Task FileModifiedInMultiFrameworkProjectOnlyReturnsASingleProject()
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                r.CreateDirectory("Sample");
                await r.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");
                return r.CreateProject(
                    "Sample/Sample.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net9.0;net8.0;netstandard2.0"); }
                );
            }
        );
        var (project, repo) = res;
        await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some new content");
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                FindMergeBase = false
            }
        );
        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var proj = Assert.Single(result.Projects);

        Assert.Equal(DiffStatus.Modified, proj.Status);
        Assert.Equal(project, proj.Path);
    }
}
