using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class ErrorTests
{
    [Fact]
    public async Task InvalidBaseCommitReturnsError()
    {
        using var repo = TestRepository.CreateEmpty();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());

        var result = await executor.GetProjectDiff(new FileInfo("doesnotexist.sln"), "SOME-INVALID-COMMIT-SHA");

        Assert.Equal(ProjectDiffExecutionStatus.BaseCommitNotFound, result.Status);
    }

    [Fact]
    public async Task InvalidHeadCommitReturnsError()
    {
        using var repo = TestRepository.CreateEmpty();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());

        var result = await executor.GetProjectDiff(new FileInfo("doesnotexist.sln"), "HEAD", "SOME-INVALID-COMMIT-SHA");

        Assert.Equal(ProjectDiffExecutionStatus.HeadCommitNotFound, result.Status);
    }
}