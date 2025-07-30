using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class ErrorTests
{
    [Fact]
    public async Task InvalidBaseCommitReturnsError()
    {
        using var repo = TestRepository.CreateEmpty();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());

        var result = await executor.GetProjectDiff(
            repo.Repository,
            new EmptyEntrypointProvider(),
            "SOME-INVALID-COMMIT-SHA",
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.BaseCommitNotFound, result.Status);
    }

    [Fact]
    public async Task InvalidHeadCommitReturnsError()
    {
        using var repo = TestRepository.CreateEmpty();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());

        var result = await executor.GetProjectDiff(
            repo.Repository,
            new EmptyEntrypointProvider(),
            "HEAD",
            "SOME-INVALID-COMMIT-SHA",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.HeadCommitNotFound, result.Status);
    }


    private sealed class EmptyEntrypointProvider : IEntrypointProvider
    {
        public Task<IEnumerable<ProjectGraphEntryPoint>> GetEntrypoints(
            string repositoryWorkingDirectory,
            MSBuildFileSystemBase fs,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(Enumerable.Empty<ProjectGraphEntryPoint>());
        }
    }
}
