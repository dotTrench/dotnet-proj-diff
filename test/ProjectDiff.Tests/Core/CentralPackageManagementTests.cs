using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;
using Task = System.Threading.Tasks.Task;

namespace ProjectDiff.Tests.Core;

public sealed class CentralPackageManagementTests
{
    
    [Fact]
    public async Task MultiTargetedFrameworkPackageIsChanged_ModifiedProjectReturned()
    {
        using var res = await TestRepository.SetupAsync(
            static async r =>
            {
                r.CreateDirectory("Core");

                await r.WriteFileAsync(
                    "Directory.Packages.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup Condition="'$(TargetFramework)'== 'net8.0'">
                            <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
                        </ItemGroup>
                    
                        <ItemGroup Condition="'$(TargetFramework)'== 'netstandard2.0'">
                            <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.0" />
                        </ItemGroup>
                    </Project>
                    """
                );
                r.CreateProject(
                    "Core/Core.csproj",
                    p =>
                    {
                        p.AddItem("PackageReference", "Microsoft.Extensions.Logging");
                        p.AddProperty("TargetFrameworks", "net8.0;netstandard2.0");
                    }
                );

                var sln = await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );

                return sln;
            }
        );

        var (sln, repo) = res;

        await repo.WriteFileAsync(
            "Directory.Packages.props",
            """
            <Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup Condition="'$(TargetFramework)'== 'net8.0'">
                    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.1" />
                </ItemGroup>
            
                <ItemGroup Condition="'$(TargetFramework)'== 'net9.0'">
                    <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.1" />
                </ItemGroup>
            </Project>
            """
        );
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                CheckPackageReferences = true
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "HEAD"
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        var project = Assert.Single(result.Projects);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }
    [Fact]
    public async Task TargetedFrameworkPackageIsChanged_ModifiedProjectReturned()
    {
        using var res = await TestRepository.SetupAsync(
            static async r =>
            {
                r.CreateDirectory("Core");

                await r.WriteFileAsync(
                    "Directory.Packages.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup Condition="'$(TargetFramework)'== 'net8.0'">
                            <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
                        </ItemGroup>
                    
                        <ItemGroup Condition="'$(TargetFramework)'== 'net9.0'">
                            <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.0" />
                        </ItemGroup>
                    </Project>
                    """
                );
                r.CreateProject(
                    "Core/Core.csproj",
                    p =>
                    {
                        p.AddItem("PackageReference", "Microsoft.Extensions.Logging");
                        p.AddProperty("TargetFramework", "net8.0");
                    }
                );

                var sln = await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );

                return sln;
            }
        );

        var (sln, repo) = res;

        await repo.WriteFileAsync(
            "Directory.Packages.props",
            """
            <Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup Condition="'$(TargetFramework)'== 'net8.0'">
                    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.1" />
                </ItemGroup>
            
                <ItemGroup Condition="'$(TargetFramework)'== 'net9.0'">
                    <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.0" />
                </ItemGroup>
            </Project>
            """
        );
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                CheckPackageReferences = true
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "HEAD"
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        var project = Assert.Single(result.Projects);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public async Task NonTargetedFrameworkPackageIsChanged_ProjectIsNotReturned()
    {
        using var res = await TestRepository.SetupAsync(
            static async r =>
            {
                r.CreateDirectory("Core");

                await r.WriteFileAsync(
                    "Directory.Packages.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup Condition="'$(TargetFramework)'== 'net8.0'">
                            <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
                        </ItemGroup>
                    
                        <ItemGroup Condition="'$(TargetFramework)'== 'net9.0'">
                            <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.0" />
                        </ItemGroup>
                    </Project>
                    """
                );
                r.CreateProject(
                    "Core/Core.csproj",
                    p =>
                    {
                        p.AddItem("PackageReference", "Microsoft.Extensions.Logging");
                        p.AddProperty("TargetFramework", "net8.0");
                    }
                );

                var sln = await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );

                return sln;
            }
        );

        var (sln, repo) = res;

        await repo.WriteFileAsync(
            "Directory.Packages.props",
            """
            <Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup Condition="'$(TargetFramework)'== 'net8.0'">
                    <PackageVersion Include="Microsoft.Extensions.Logging" Version="8.0.0" />
                </ItemGroup>
            
                <ItemGroup Condition="'$(TargetFramework)'== 'net9.0'">
                    <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.1" />
                </ItemGroup>
            </Project>
            """
        );
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                CheckPackageReferences = true
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "HEAD"
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task NonConsumedPackageVersionIsChanged_ProjectIsNotReturned()
    {
        using var res = await TestRepository.SetupAsync(
            static async r =>
            {
                r.CreateDirectory("Core");

                await r.WriteFileAsync(
                    "Directory.Packages.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup>
                            <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="1.2.3" />
                            <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
                        </ItemGroup>
                    </Project>
                    """
                );
                r.CreateProject(
                    "Core/Core.csproj",
                    p =>
                    {
                        p.AddItem("PackageReference", "Newtonsoft.Json");
                        p.AddProperty("TargetFramework", "net8.0");
                    }
                );

                var sln = await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );

                return sln;
            }
        );

        var (sln, repo) = res;

        await repo.WriteFileAsync(
            "Directory.Packages.props",
            """
            <Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="1.2.4" />
                    <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
                </ItemGroup>
            </Project>
            """
        );
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                CheckPackageReferences = true
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "HEAD"
        );
        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);

        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task ConsumedPackageVersionIsChanged_ModifiedProjectIsReturned()
    {
        using var res = await TestRepository.SetupAsync(
            static async r =>
            {
                r.CreateDirectory("Core");

                await r.WriteFileAsync(
                    "Directory.Packages.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                        </PropertyGroup>
                        <ItemGroup>
                            <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="1.2.3" />
                            <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
                        </ItemGroup>
                    </Project>
                    """
                );
                var project = r.CreateProject(
                    "Core/Core.csproj",
                    p =>
                    {
                        p.AddItem("PackageReference", "Newtonsoft.Json");
                        p.AddProperty("TargetFramework", "net8.0");
                    }
                );

                var sln = await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );

                return (sln, project);
            }
        );

        var ((sln, projectPath), repo) = res;

        await repo.WriteFileAsync(
            "Directory.Packages.props",
            """
            <Project>
                <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                </PropertyGroup>
                <ItemGroup>
                    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="1.2.3" />
                    <PackageVersion Include="Newtonsoft.Json" Version="13.0.2" />
                </ItemGroup>
            </Project>
            """
        );
        var executor = new ProjectDiffExecutor(
            new ProjectDiffExecutorOptions
            {
                CheckPackageReferences = true
            }
        );
        var result = await executor.GetProjectDiff(
            new FileInfo(sln),
            "HEAD"
        );

        Assert.Equal(ProjectDiffExecutionStatus.Success, result.Status);
        var project = Assert.Single(result.Projects);
        Assert.Equal(DiffStatus.Modified, project.Status);
        Assert.Equal(projectPath, project.Path);
    }
}