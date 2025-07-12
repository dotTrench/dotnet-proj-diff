using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;

namespace ProjectDiff.Core.Entrypoints;

public sealed class DirectoryScanEntrypointProvider : IEntrypointProvider
{
    private readonly string _directory;

    public DirectoryScanEntrypointProvider(string directory)
    {
        _directory = directory;
    }

    public Task<IEnumerable<ProjectGraphEntryPoint>> GetEntrypoints(
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        var entrypoints = fs.EnumerateFiles(_directory, "*.csproj", SearchOption.AllDirectories)
            .Select(it => new ProjectGraphEntryPoint(it));


        return Task.FromResult(entrypoints);
    }
}