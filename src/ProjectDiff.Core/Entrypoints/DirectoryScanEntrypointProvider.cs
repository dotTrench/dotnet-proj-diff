using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ProjectDiff.Core.Entrypoints;

public sealed class DirectoryScanEntrypointProvider : IEntrypointProvider
{
    private readonly ILogger<DirectoryScanEntrypointProvider> _logger;


    public DirectoryScanEntrypointProvider(ILogger<DirectoryScanEntrypointProvider>? logger = null)
    {
        _logger = logger ?? NullLogger<DirectoryScanEntrypointProvider>.Instance;
    }

    public Task<IEnumerable<ProjectGraphEntryPoint>> GetEntrypoints(
        string repositoryWorkingDirectory,
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug("Scanning directory '{Directory}' for project files", repositoryWorkingDirectory);
        var entrypoints = fs.EnumerateFiles(repositoryWorkingDirectory, "*.csproj", SearchOption.AllDirectories)
            .Select(it => new ProjectGraphEntryPoint(it));


        return Task.FromResult(entrypoints);
    }
}
