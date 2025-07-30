using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class MultiFrameworkTests
{
    [Fact]
    public async Task ProjectImportedInSingleFrameworkShouldBeIncludedInOutput()
    {
        using var repo = await TestRepository.SetupAsync(r =>
            {
                r.CreateDirectory("Core");
                r.CreateDirectory("Application");

                r.CreateProject(
                    "Core/Core.csproj",
                    p => { p.AddProperty("TargetFramework", "net9.0"); }
                );

                r.CreateProject(
                    "Application/Application.csproj",
                    p =>
                    {
                        p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0");
                        var reference = p.AddItem("ProjectReference", "../Core/Core.csproj");

                        reference.Condition = "'$(TargetFramework)' == 'net8.0'";
                    }
                );
                return Task.CompletedTask;
            }
        );

        await repo.WriteAllTextAsync("Core/Class.cs", "// Some content");
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                FindMergeBase = false
            }
        );
        var result = await executor.GetProjectDiff(
            repo.Repository,
            new DirectoryScanEntrypointProvider(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        Assert.Collection(
            result.Projects,
            p =>
            {
                Assert.Equal("Core", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            },
            p =>
            {
                Assert.Equal("Application", p.Name);
                Assert.Equal(DiffStatus.ReferenceChanged, p.Status);
            }
        );
    }

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
            repo,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var proj = Assert.Single(result.Projects);

        Assert.Equal(DiffStatus.Modified, proj.Status);
        Assert.Equal(project, proj.Path);
    }
}
