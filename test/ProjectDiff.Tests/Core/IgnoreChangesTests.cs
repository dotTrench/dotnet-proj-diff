using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class IgnoreChangesTests
{
    [Fact]
    public async Task IgnoresModifiedFiles()
    {
        using var res = await TestRepository.SetupAsync(
            async r =>
            {
                r.CreateDirectory("Core");
                r.CreateProject("Core/Core.csproj");

                r.WriteAllText("Core/Sample.cs", "// Some file here");

                return await r.CreateSolutionAsync(
                    "MySln.sln",
                    model => { model.AddProject("Core/Core.csproj"); }
                );
            }
        );


        var (sln, repo) = res;

        repo.WriteAllText("Core/Sample.cs", "// New content here");

        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                IgnoreChangedFiles = [new FileInfo(repo.GetPath("Core/Sample.cs"))]
            }
        );
        var diff = await executor.GetProjectDiff(new FileInfo(sln));
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);
        Assert.Empty(diff.ChangedFiles);
    }


    [Fact]
    public async Task IgnoresAddedFiles()
    {
        using var res = await TestRepository.SetupAsync(
            async r =>
            {
                r.CreateDirectory("Core");
                r.CreateProject("Core/Core.csproj");


                return await r.CreateSolutionAsync(
                    "MySln.sln",
                    model => { model.AddProject("Core/Core.csproj"); }
                );
            }
        );

        var (sln, repo) = res;

        repo.WriteAllText("Core/MyClass.cs", "// Some content here");
        repo.WriteAllText("README.md", "Hello there"); // 
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                IgnoreChangedFiles = [new FileInfo(repo.GetPath("Core/MyClass.cs"))]
            }
        );

        var diff = await executor.GetProjectDiff(new FileInfo(sln));
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);

        var changedFile = Assert.Single(diff.ChangedFiles);

        Assert.Matches("README.md", changedFile);
    }


    [Fact]
    public async Task IgnoresDeletedFiles()
    {
        using var res = await TestRepository.SetupAsync(
            async r =>
            {
                r.CreateDirectory("Core");
                r.CreateProject("Core/Core.csproj");

                r.WriteAllText("Core/Sample.cs", "// Some file here");

                return await r.CreateSolutionAsync(
                    "MySln.sln",
                    model => { model.AddProject("Core/Core.csproj"); }
                );
            }
        );
        var (sln, repo) = res;

        repo.DeleteFile("Core/Sample.cs");
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                IgnoreChangedFiles = [new FileInfo(repo.GetPath("Core/Sample.cs"))]
            }
        );
        var diff = await executor.GetProjectDiff(new FileInfo(sln));
        Assert.Equal(ProjectDiffExecutionStatus.Success, diff.Status);

        Assert.Empty(diff.Projects);
    }
}