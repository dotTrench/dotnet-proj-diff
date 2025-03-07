using LibGit2Sharp;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace ProjectDiff.Core;

public static class ProjectGraphFactory
{
    public static async Task<ProjectGraph> BuildForGitTree(
        Repository repository,
        Tree tree,
        FileInfo solutionFile,
        CancellationToken cancellationToken = default
    )
    {
        using var projectCollection = new ProjectCollection();

        var fs = new GitTreeFileSystem(
            new DirectoryInfo(repository.Info.WorkingDirectory),
            tree,
            projectCollection,
            []
        );
        if (!fs.FileExists(solutionFile.FullName))
        {
            throw new InvalidOperationException("Solution file does not exist.");
        }

        await using var stream = fs.GetFileStream(
            solutionFile.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );

        var entrypoints = (await GetProjectEntrypoints(solutionFile, stream, cancellationToken))
            .Select(it => new ProjectGraphEntryPoint(it))
            .ToList();

        var graph = new ProjectGraph(
            entrypoints,
            projectCollection,
            (path, globalProperties, collection) =>
            {
                var proj = fs.LoadProject(path, globalProperties, collection);

                proj.MarkDirty();

                return proj.CreateProjectInstance(
                    ProjectInstanceSettings.None,
                    EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared, fs)
                );
            },
            cancellationToken
        );

        return graph;
    }

    public static async Task<ProjectGraph> BuildForWorkingDirectory(
        FileInfo solutionFile,
        CancellationToken cancellationToken = default
    )
    {
        using var projectCollection = new ProjectCollection();

        await using var fs = solutionFile.OpenRead();

        var entrypoints = (await GetProjectEntrypoints(solutionFile, fs, cancellationToken))
            .Select(it => new ProjectGraphEntryPoint(it));
        var graph = new ProjectGraph(
            entrypoints,
            projectCollection,
            (path, properties, collection) =>
            {
                return ProjectInstance.FromFile(
                    path,
                    new ProjectOptions
                    {
                        ProjectCollection = collection,
                        GlobalProperties = properties,
                    }
                );
            },
            cancellationToken
        );

        return graph;
    }


    private static async Task<IEnumerable<string>> GetProjectEntrypoints(
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
                    .Select(
                        it =>
                            Path.GetFullPath(it.FilePath, solutionFile.DirectoryName!)
                    );
            }
            case ".slnx":
            {
                var solutionModel = await SolutionSerializers.SlnXml.OpenAsync(stream, cancellationToken);

                return solutionModel.SolutionProjects
                    .Select(
                        it =>
                            Path.GetFullPath(it.FilePath, solutionFile.DirectoryName!)
                    );
            }
            default:
                throw new NotSupportedException($"Solution file extension {solutionFile.Extension} not supported");
        }
    }
}