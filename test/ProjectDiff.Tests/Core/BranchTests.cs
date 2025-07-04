﻿using ProjectDiff.Core;
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
        await repo.WriteFileAsync("Sample/MyClass.cs", "// Some content");
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());

        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
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
                await repo.WriteFileAsync("Sample/MyClass.cs", "// Some content");
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
            new FileInfo(sln),
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
                var sln = await repo.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
                var project = repo.CreateProject(
                    "Sample/Sample.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                await repo.WriteFileAsync("Sample/MyClass.cs", "// Some content");
                return (sln, project);
            }
        );
        var ((sln, project), repo) = res;

        repo.CreateAndCheckoutBranch("feature");
        await repo.WriteFileAsync("Sample/MyClass.cs", "// Some new content");
        repo.StageAndCommitAllChanges();
        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
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
        using var res = await TestRepository.SetupAsync(static async repo =>
            {
                var sln = await repo.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Core/Core.csproj"));
                var project = repo.CreateProject(
                    "Core/Core.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                return (sln, project);
            }
        );
        var ((sln, project), repo) = res;
        repo.CreateBranch("feature"); // Create the branch without checking it out

        await repo.WriteFileAsync("Core/MyClass.cs", "// Some content");
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
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
        using var res = await TestRepository.SetupAsync(static async repo =>
            {
                var sln = await repo.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Core/Core.csproj"));
                var project = repo.CreateProject(
                    "Core/Core.csproj",
                    p => { p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0"); }
                );
                return (sln, project);
            }
        );
        var ((sln, project), repo) = res;
        repo.CreateBranch("feature"); // Create the branch without checking it out

        await repo.WriteFileAsync("Core/MyClass.cs", "// Some content");
        repo.StageAndCommitAllChanges();

        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                FindMergeBase = true
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "master",
            "feature",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        Assert.Empty(result.Projects);
    }
}