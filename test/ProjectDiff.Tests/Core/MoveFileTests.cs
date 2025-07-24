using LibGit2Sharp;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class MoveFileTests
{
    [Fact]
    public async Task MoveFileToReferencedProject()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                r.CreateDirectory("ProjectA");
                r.CreateProject("ProjectA/ProjectA.csproj");

                r.CreateDirectory("ProjectB");
                r.CreateProject(
                    "ProjectB/ProjectB.csproj",
                    p => p.AddItem("ProjectReference", "../ProjectA/ProjectA.csproj")
                );

                await r.WriteAllTextAsync("ProjectB/FileA.cs", "File A content");
            }
        );
        Commands.Move(
            repo.Repository,
            "ProjectB/FileA.cs",
            "ProjectA/FileA.cs"
        );
        var projects = await GetDiffProjects(repo);

        Assert.Collection(
            projects,
            p =>
            {
                Assert.Equal("ProjectA", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            },
            p =>
            {
                Assert.Equal("ProjectB", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            }
        );
    }

    [Fact]
    public async Task MoveFileFromReferencedProjects()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                r.CreateDirectory("ProjectA");
                r.CreateProject("ProjectA/ProjectA.csproj");
                await r.WriteAllTextAsync("ProjectA/FileA.cs", "File A content");

                r.CreateDirectory("ProjectB");
                r.CreateProject(
                    "ProjectB/ProjectB.csproj",
                    p => p.AddItem("ProjectReference", "../ProjectA/ProjectA.csproj")
                );
            }
        );
        Commands.Move(
            repo.Repository,
            "ProjectA/FileA.cs",
            "ProjectB/FileA.cs"
        );
        var projects = await GetDiffProjects(repo);

        Assert.Collection(
            projects,
            p =>
            {
                Assert.Equal("ProjectA", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            },
            p =>
            {
                Assert.Equal("ProjectB", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            }
        );
    }


    [Fact]
    public async Task MovingFileBetweenTwoUnreleatedProjects()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                r.CreateDirectory("ProjectA");
                r.CreateProject("ProjectA/ProjectA.csproj");
                await r.WriteAllTextAsync("ProjectA/FileA.cs", "File A content");

                r.CreateDirectory("ProjectB");
                r.CreateProject("ProjectB/ProjectB.csproj");
            }
        );

        Commands.Move(
            repo.Repository,
            "ProjectA/FileA.cs",
            "ProjectB/FileA.cs"
        );
        var projects = await GetDiffProjects(repo);
        Assert.Collection(
            projects,
            p =>
            {
                Assert.Equal("ProjectA", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            },
            p =>
            {
                Assert.Equal("ProjectB", p.Name);
                Assert.Equal(DiffStatus.Modified, p.Status);
            }
        );
    }

    private static async Task<IEnumerable<DiffProject>> GetDiffProjects(TestRepository repo)
    {
        var options = new ProjectDiffExecutorOptions
        {
            FindMergeBase = false,
            IgnoreChangedFiles = []
        };


        var executor = new ProjectDiffExecutor(options);
        var result = await executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        return result.Projects;
    }
}
