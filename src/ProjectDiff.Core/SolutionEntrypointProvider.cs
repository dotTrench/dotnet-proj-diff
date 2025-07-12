using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace ProjectDiff.Core;

public sealed class SolutionEntrypointProvider : IEntrypointProvider
{
    private readonly FileInfo _solution;

    public SolutionEntrypointProvider(FileInfo solution)
    {
        _solution = solution;
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


    private static async Task<IEnumerable<ProjectGraphEntryPoint>> GetProjectEntrypoints(
        FileInfo solutionFile,
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        switch (solutionFile.Extension)
        {
            case ".sln":
            {
                var solutionModel = await SolutionSerializers.SlnFileV12.OpenAsync(stream, cancellationToken);

                return solutionModel.SolutionProjects
                    .Select(it =>
                        new ProjectGraphEntryPoint(Path.GetFullPath(it.FilePath, solutionFile.DirectoryName!))
                    );
            }
            case ".slnx":
            {
                var solutionModel = await SolutionSerializers.SlnXml.OpenAsync(stream, cancellationToken);

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