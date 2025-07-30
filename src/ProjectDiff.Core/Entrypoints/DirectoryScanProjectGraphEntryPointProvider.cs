using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ProjectDiff.Core.Entrypoints;

public sealed class DirectoryScanProjectGraphEntryPointProvider : IProjectGraphEntryPointProvider
{
    private readonly ILogger<DirectoryScanProjectGraphEntryPointProvider> _logger;
    private readonly string _repositoryWorkingDirectory;


    public DirectoryScanProjectGraphEntryPointProvider(
        string repositoryWorkingDirectory,
        ILogger<DirectoryScanProjectGraphEntryPointProvider>? logger = null
    )
    {
        _repositoryWorkingDirectory = repositoryWorkingDirectory;
        _logger = logger ?? NullLogger<DirectoryScanProjectGraphEntryPointProvider>.Instance;
    }

    public Task<IEnumerable<ProjectGraphEntryPoint>> GetEntryPoints(
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        _logger.LogDebug("Scanning directory '{Directory}' for project files", _repositoryWorkingDirectory);
        var entrypoints = fs.EnumerateFiles(_repositoryWorkingDirectory, "*.csproj", SearchOption.AllDirectories)
            .Select(it => new ProjectGraphEntryPoint(it));


        return Task.FromResult(entrypoints);
    }
}
