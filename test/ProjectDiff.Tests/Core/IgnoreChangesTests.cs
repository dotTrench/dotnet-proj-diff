using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class IgnoreChangesTests
{
    [Fact]
    public async Task IgnoresModifiedFiles()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                r.CreateDirectory("Core");
                r.CreateProject("Core/Core.csproj");

                await r.WriteAllTextAsync("Core/Sample.cs", "// Some file here");
            }
        );

        await repo.WriteAllTextAsync("Core/Sample.cs", "// New content here");

        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                IgnoreChangedFiles = [new FileInfo(repo.GetPath("Core/Sample.cs"))]
            }
        );
        var diff = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);
        Assert.Empty(diff.ChangedFiles);
    }


    [Fact]
    public async Task IgnoresAddedFiles()
    {
        using var repo = await TestRepository.SetupAsync(r =>
            {
                r.CreateDirectory("Core");
                r.CreateProject("Core/Core.csproj");

                return Task.CompletedTask;
            }
        );

        await repo.WriteAllTextAsync("Core/MyClass.cs", "// Some content here");
        await repo.WriteAllTextAsync("README.md", "Hello there"); // 
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                IgnoreChangedFiles = [new FileInfo(repo.GetPath("Core/MyClass.cs"))]
            }
        );

        var diff = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);

        var changedFile = Assert.Single(diff.ChangedFiles);

        Assert.Matches("README.md", changedFile);
    }


    [Fact]
    public async Task IgnoresDeletedFiles()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                r.CreateDirectory("Core");
                r.CreateProject("Core/Core.csproj");

                await r.WriteAllTextAsync("Core/Sample.cs", "// Some file here");
            }
        );

        repo.DeleteFile("Core/Sample.cs");
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                IgnoreChangedFiles = [new FileInfo(repo.GetPath("Core/Sample.cs"))]
            }
        );
        var diff = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);
    }
}