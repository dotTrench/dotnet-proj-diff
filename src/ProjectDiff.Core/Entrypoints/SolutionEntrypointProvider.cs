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
        string repositoryWorkingDirectory,
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        if (!fs.FileExists(_solution.FullName))
        {
            _logger.LogError("Could not find the solution file {SolutionFile} in the file system", _solution.FullName);
            throw new FileNotFoundException("Could not find the solution file in the file system", _solution.FullName);
        }

        await using var stream = fs.GetFileStream(
            _solution.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );


        var solutionModel = _solution.Extension switch
        {
            ".sln" => await SolutionSerializers.SlnFileV12.OpenAsync(stream, cancellationToken),
            ".slnx" => await SolutionSerializers.SlnXml.OpenAsync(stream, cancellationToken),
            _ => throw new NotSupportedException($"Solution file extension {_solution.Extension} is not supported")
        };
        return solutionModel.SolutionProjects
            .Select(it =>
                new ProjectGraphEntryPoint(Path.GetFullPath(it.FilePath, _solution.DirectoryName!))
            );
    }
}
