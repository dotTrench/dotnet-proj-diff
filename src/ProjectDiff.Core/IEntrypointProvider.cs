using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;

namespace ProjectDiff.Core;

public interface IEntrypointProvider
{
    Task<IEnumerable<ProjectGraphEntryPoint>> GetEntrypoints(MSBuildFileSystemBase fs, CancellationToken cancellationToken);
}