using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core;
using ProjectDiff.Core.Entrypoints;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class DirectoryBuildPropsTests
{
    [Fact]
    public async Task ModifyingParentDirectoryBuildProps_ShouldNotAffectProjectInSubDirectory()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                await r.WriteAllTextAsync(
                    "Directory.Build.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <MyCustomProperty>Value</MyCustomProperty>
                        </PropertyGroup>
                    </Project>
                    """
                );
                r.CreateDirectory("Sample");
                await r.WriteAllTextAsync(
                    "Sample/Directory.Build.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <SomeOtherProperty>Value</SomeOtherProperty>
                        </PropertyGroup>
                    </Project>
                    """
                );
                r.CreateProject("Sample/Sample.csproj");
            }
        );

        await repo.WriteAllTextAsync(
            "Directory.Build.props",
            """
            <Project>
                <PropertyGroup>
                    <MyCustomProperty>SomeNewValue</MyCustomProperty>
                </PropertyGroup>
            </Project>
            """
        );

        var result = await GetProjectDiff(repo);
        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task DirectoryBuildProps_AffectsProjects_IfImportedByNestedDirectoryBuildProps()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                await r.WriteAllTextAsync(
                    "Directory.Build.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <MyCustomProperty>Value</MyCustomProperty>
                        </PropertyGroup>
                    </Project>
                    """
                );
                r.CreateDirectory("Sample");
                await r.WriteAllTextAsync(
                    "Sample/Directory.Build.props",
                    """
                    <Project>
                        <!-- This is a nested Directory.Build.props file -->
                        <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
                        <PropertyGroup>
                            <SomeOtherProperty>Value</SomeOtherProperty>
                        </PropertyGroup>
                    </Project>
                    """
                );
                r.CreateProject("Sample/Sample.csproj");
            }
        );


        await repo.WriteAllTextAsync(
            "Directory.Build.props",
            """
            <Project>
                <PropertyGroup>
                    <MyCustomProperty>SomeNewValue</MyCustomProperty>
                </PropertyGroup>
            </Project>
            """
        );

        var result = await GetProjectDiff(repo);

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        var project = Assert.Single(result.Projects);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public async Task AddingDirectoryBuildProps_ShouldOnlyAffectProjectsInDirectory()
    {
        using var repo = await TestRepository.SetupAsync(r =>
            {
                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");
                r.CreateDirectory("Other");
                r.CreateProject("Other/Other.csproj");
                return Task.CompletedTask;
            }
        );

        await repo.WriteAllTextAsync(
            "Sample/Directory.Build.props",
            """
            <Project>
                <PropertyGroup>
                    <MyCustomProperty>Value</MyCustomProperty>
                </PropertyGroup>
            </Project>
            """
        );

        var result = await GetProjectDiff(repo);
        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var project = Assert.Single(result.Projects);

        Assert.Equal(repo.GetPath("Sample", "Sample.csproj"), project.Path);
    }

    [Fact]
    public async Task AddingDirectoryBuildProps_ShouldAffectProjects()
    {
        using var repo = await TestRepository.SetupAsync(r =>
            {
                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");
                return Task.CompletedTask;
            }
        );

        await repo.WriteAllTextAsync(
            "Directory.Build.props",
            """
            <Project>
                <PropertyGroup>
                    <MyCustomProperty>Value</MyCustomProperty>
                </PropertyGroup>
            </Project>
            """
        );

        var result = await GetProjectDiff(repo);

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        var project = Assert.Single(result.Projects);

        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public async Task ModifyingDirectoryBuildProps_ShouldAffectProjects()
    {
        using var repo = await TestRepository.SetupAsync(async r =>
            {
                await r.WriteAllTextAsync(
                    "Directory.Build.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <MyCustomProperty>Value</MyCustomProperty>
                        </PropertyGroup>
                    </Project>
                    """
                );

                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");
            }
        );

        await repo.WriteAllTextAsync(
            "Directory.Build.props",
            """
            <Project>
                <PropertyGroup>
                    <MyCustomProperty>SomeNewValue</MyCustomProperty>
                </PropertyGroup>
            </Project>
            """
        );
        var result = await GetProjectDiff(repo);

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        var project = Assert.Single(result.Projects);

        Assert.Equal(DiffStatus.Modified, project.Status);
    }


    private static Task<ProjectDiffResult> GetProjectDiff(TestRepository repo)
    {
        var executor = new ProjectDiffExecutor(new ProjectDiffExecutorOptions());
        return executor.GetProjectDiff(
            repo.WorkingDirectory,
            new DirectoryScanEntrypointProvider(NullLogger<DirectoryScanEntrypointProvider>.Instance),
            cancellationToken: TestContext.Current.CancellationToken
        );
    }
}
