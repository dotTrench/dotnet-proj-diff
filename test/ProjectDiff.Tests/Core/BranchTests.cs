using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class BranchTests
{
    [Fact]
    public async Task AddProjectInNewBranch()
    {
        using var repo = await TestRepository.SetupAsync(static async repo =>
            {
                await repo.CreateSolutionAsync("Sample.sln", _ => { }); // Create an empty solution
            }
        );

        repo.CreateAndCheckoutBranch("feature");
        var sln = await repo.UpdateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
        var project = repo.CreateProject(
            "Sample/Sample.csproj",
            p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
        );
        await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions(), NullLoggerFactory.Instance);

        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new SolutionEntrypointProvider(new FileInfo(sln), NullLogger<SolutionEntrypointProvider>.Instance),
            "master",
            "feature",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var diffProject = Assert.Single(result.Projects);

        Assert.Equal(project, diffProject.Path);
        Assert.Equal(DiffStatus.Added, diffProject.Status);
    }

    [Fact]
    public async Task RemoveProjectInNewBranch()
    {
        using var res = await TestRepository.SetupAsync(static async repo =>
            {
                var sln = await repo.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
                var project = repo.CreateProject(
                    "Sample/Sample.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");
                return (sln, project);
            }
        );
        var ((sln, project), repo) = res;
        repo.CreateAndCheckoutBranch("feature");
        repo.DeleteDirectory("Sample", true);
        await repo.UpdateSolutionAsync(
            "Sample.sln",
            static sln => sln.RemoveProject(sln.FindProject("Sample/Sample.csproj")!)
        );
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());
        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new SolutionEntrypointProvider(new FileInfo(sln), NullLogger<SolutionEntrypointProvider>.Instance),
            "master",
            "feature",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var diffProject = Assert.Single(result.Projects);

        Assert.Equal(project, diffProject.Path);
        Assert.Equal(DiffStatus.Removed, diffProject.Status);
    }

    [Fact]
    public async Task ModifyProjectInNewBranch()
    {
        using var res = await TestRepository.SetupAsync(static async repo =>
            {
                var project = repo.CreateProject(
                    "Sample/Sample.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");
                return project;
            }
        );
        var (project, repo) = res;

        repo.CreateAndCheckoutBranch("feature");
        await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some new content");
        repo.StageAndCommitAllChanges();
        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());
        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            "master",
            "feature",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var diffProject = Assert.Single(result.Projects);
        Assert.Equal(project, diffProject.Path);
        Assert.Equal(DiffStatus.Modified, diffProject.Status);
    }

    [Fact]
    public async Task ModifyProjectInBaseBranch_WithNoMergeBaseOption()
    {
        using var res = await TestRepository.SetupAsync(static repo =>
            {
                var project = repo.CreateProject(
                    "Core/Core.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                return Task.FromResult(project);
            }
        );
        var (project, repo) = res;
        repo.CreateBranch("feature"); // Create the branch without checking it out

        await repo.WriteAllTextAsync("Core/MyClass.cs", "// Some content");
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());
        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            "master",
            "feature",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var diffProject = Assert.Single(result.Projects);
        Assert.Equal(project, diffProject.Path);
        Assert.Equal(DiffStatus.Modified, diffProject.Status);
    }


    [Fact]
    public async Task ModifyProjectInBaseBranch_WithMergeBaseOption()
    {
        using var repo = await TestRepository.SetupAsync(static repo =>
            {
                repo.CreateProject(
                    "Core/Core.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                return Task.CompletedTask;
            }
        );
        repo.CreateBranch("feature"); // Create the branch without checking it out

        await repo.WriteAllTextAsync("Core/MyClass.cs", "// Some content");
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                FindMergeBase = true
            }
        );
        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            "master",
            "feature",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        Assert.Empty(result.Projects);
    }
}
