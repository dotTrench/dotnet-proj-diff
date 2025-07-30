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

        var diff = await GetProjectDiff(repo, ["Core/Sample.cs"]);
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

        var diff = await GetProjectDiff(repo, ["Core/MyClass.cs"]);
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
        var diff = await GetProjectDiff(repo, ["Core/Sample.cs"]);
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);
    }

    private static async Task<ProjectDiffResult> GetProjectDiff(TestRepository repo, IEnumerable<string> ignoredFiles)
    {
        var files = ignoredFiles.Select(it => new FileInfo(repo.GetPath(it))).ToArray();
        var options = new ProjectDiffExecutorOptions
        {
            IgnoreChangedFiles = files,
        };
        var executor = new ProjectDiffExecutor(options);
        return await executor.GetProjectDiff(
            repo.Repository,
            new DirectoryScanProjectGraphEntryPointProvider(
                repo.WorkingDirectory,
                NullLogger<DirectoryScanProjectGraphEntryPointProvider>.Instance
            ),
            cancellationToken: TestContext.Current.CancellationToken
        );
    }
}
