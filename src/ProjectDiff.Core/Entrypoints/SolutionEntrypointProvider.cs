using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace ProjectDiff.Core.Entrypoints;

public sealed class SolutionEntrypointProvider : IEntrypointProvider
{
    private readonly FileInfo _solution;
    private readonly ILogger<SolutionEntrypointProvider> _logger;

    public SolutionEntrypointProvider(FileInfo solution, ILogger<SolutionEntrypointProvider> logger)
    {
        _solution = solution;
        _logger = logger;
    }

    public async Task<IEnumerable<ProjectGraphEntryPoint>> GetEntrypoints(
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        await using var stream = fs.GetFileStream(
            _solution.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );


        return await GetProjectEntrypoints(_solution, stream, cancellationToken);
    }


    private async Task<IEnumerable<ProjectGraphEntryPoint>> GetProjectEntrypoints(
        FileInfo solutionFile,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        switch (solutionFile.Extension)
        {
            case ".sln":
            {
                _logger.LogDebug("Reading {SolutionFile} as a .sln file", solutionFile.FullName);
                var solutionModel = await SolutionSerializers.SlnFileV12.OpenAsync(stream, cancellationToken);

                _logger.LogDebug(
                    "Found {ProjectCount} projects in solution {SolutionFile}",
                    solutionModel.SolutionProjects.Count,
                    solutionFile.FullName
                );
                return solutionModel.SolutionProjects
                    .Select(it =>
                        new ProjectGraphEntryPoint(Path.GetFullPath(it.FilePath, solutionFile.DirectoryName!))
                    );
            }
            case ".slnx":
            {
                _logger.LogDebug("Reading {SolutionFile} as a .slnx file", solutionFile.FullName);
                var solutionModel = await SolutionSerializers.SlnXml.OpenAsync(stream, cancellationToken);

                _logger.LogDebug(
                    "Found {ProjectCount} projects in solution {SolutionFile}",
                    solutionModel.SolutionProjects.Count,
                    solutionFile.FullName
                );
                return solutionModel.SolutionProjects
                    .Select(it =>
                        new ProjectGraphEntryPoint(Path.GetFullPath(it.FilePath, solutionFile.DirectoryName!))
                    );
            }
            default:
                throw new NotSupportedException($"Solution file extension {solutionFile.Extension} not supported");
        }
    }
}