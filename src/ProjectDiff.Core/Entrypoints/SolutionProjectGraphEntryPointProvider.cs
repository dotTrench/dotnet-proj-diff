using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace ProjectDiff.Core.Entrypoints;

public sealed class SolutionProjectGraphEntryPointProvider : IProjectGraphEntryPointProvider
{
    private readonly FileInfo _solution;
    private readonly ILogger<SolutionProjectGraphEntryPointProvider> _logger;

    public SolutionProjectGraphEntryPointProvider(
        FileInfo solution,
        ILogger<SolutionProjectGraphEntryPointProvider> logger
    )
    {
        _solution = solution;
        _logger = logger;
    }

    public async Task<IEnumerable<ProjectGraphEntryPoint>> GetEntryPoints(
        MSBuildFileSystemBase fs,
        CancellationToken cancellationToken
    )
    {
        if (!fs.FileExists(_solution.FullName))
        {
            _logger.LogWarning("Could not find the solution file {SolutionFile} in the file system, assuming empty", _solution.FullName);
            return [];
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
