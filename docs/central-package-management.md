# Central Package Management

dotnet-proj-diff treats Directory.Packages.props as any other Directory.*.props file. This causes issues when using
CentralPackageManagement as any changes to the Directory.Packages.props file will cause all projects using the file to
be included in the diff, regardless of if the actual project is affected by the changes in PackageVersion. A simple way
to mitigate this would be to
use [NuGet lock files](https://devblogs.microsoft.com/nuget/enable-repeatable-package-restores-using-a-lock-file/) and
ignoring changes to `Directory.Packages.props` in the diff using `--ignore-changed-file=Directory.Packages.props`.

## Example

Directory.Packages.props

```xml

<Project>
    <PropertyGroup>
        <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
        <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
        <!-- Ensure we restore in locked mode during CI builds -->
        <RestoreLockedMode Condition="'$(ContinuousIntegrationBuild)' == 'true'">true</RestoreLockedMode>
    </PropertyGroup>

    <!-- TF_BUILD is for Azure DevOps -->
    <!-- GITHUB_ACTIONS is for, well GitHub Actions -->
    <PropertyGroup Condition="'$(TF_BUILD)' == 'true' or '$(GITHUB_ACTIONS)' == 'true'">
        <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    </PropertyGroup>

    <ItemGroup>
        <!-- Your package versions here -->
        <PackageVersion Include="Newtonsoft.Json" Version="13.0.1"/>
    </ItemGroup>
</Project>
```
