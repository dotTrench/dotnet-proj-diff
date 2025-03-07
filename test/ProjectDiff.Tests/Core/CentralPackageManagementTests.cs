using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;
using Task = System.Threading.Tasks.Task;

namespace ProjectDiff.Tests.Core;

public sealed class CentralPackageManagementTests
{
    [Fact]
    public async Task NonConsumedPackageVersionIsChanged()
    {
        using var res = await TestRepository.SetupAsync(
            async r =>
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

                return await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );
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
        await Verify(result)
            .ScrubLinesWithReplace(x => x.Replace(repo.WorkingDirectory, "{RepositoryPath}"));
    }

    [Fact]
    public async Task ConsumedPackageVersionIsChanged()
    {
        using var res = await TestRepository.SetupAsync(
            async r =>
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

                return await r.CreateSolutionAsync(
                    "MySln.sln",
                    x => { x.AddProject("Core/Core.csproj"); }
                );
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
        await Verify(result)
            .ScrubLinesWithReplace(x => x.Replace(repo.WorkingDirectory, "{RepositoryPath}"));
    }
}