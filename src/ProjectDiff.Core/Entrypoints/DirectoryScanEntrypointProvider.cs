using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;

namespace ProjectDiff.Core.Entrypoints;

public sealed class DirectoryScanEntrypointProvider : IEntrypointProvider
{
    private readonly string _directory;
    private readonly ILogger<DirectoryScanEntrypointProvider> _logger;

    public DirectoryScanEntrypointProvider(string directory, ILogger<DirectoryScanEntrypointProvider> logger)
    {
        _directory = directory;
        _logger = logger;
    }

    public Task<IEnumerable<ProjectGraphEntryPoint>> GetEntrypoints(
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug("Scanning directory '{Directory}' for project files", _directory);
        var entrypoints = fs.EnumerateFiles(_directory, "*.csproj", SearchOption.AllDirectories)
            .Select(it => new ProjectGraphEntryPoint(it));


        return Task.FromResult(entrypoints);
    }
}