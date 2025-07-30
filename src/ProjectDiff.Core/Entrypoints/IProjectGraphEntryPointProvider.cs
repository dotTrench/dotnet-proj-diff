using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;

namespace ProjectDiff.Core.Entrypoints;

public interface IProjectGraphEntryPointProvider
{
    Task<IEnumerable<ProjectGraphEntryPoint>> GetEntryPoints(
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    );
}
