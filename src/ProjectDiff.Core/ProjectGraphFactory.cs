using LibGit2Sharp;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using ProjectDiff.Core.Entrypoints;

namespace ProjectDiff.Core;

public static class ProjectGraphFactory
{
    public static async Task<ProjectGraph> BuildForGitTree(
        Repository repository,
        Tree tree,
        IEntrypointProvider entrypointProvider,
        CancellationToken cancellationToken = default
    )
    {
        using var projectCollection = new ProjectCollection();

        var fs = new GitTreeFileSystem(
            repository,
            tree,
            projectCollection,
            []
        );

        fs.LazyLoadProjects = false;
        var entrypoints = await entrypointProvider.GetEntrypoints(fs, cancellationToken);
        fs.LazyLoadProjects = true;

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
        IEntrypointProvider solutionFile,
        CancellationToken cancellationToken = default
    )
    {
        using var projectCollection = new ProjectCollection();

        var fs = new DefaultFileSystem();
        var entrypoints = (await solutionFile.GetEntrypoints(fs, cancellationToken)).ToList();
        var graph = new ProjectGraph(
            entrypoints,
            projectCollection,
            static (path, properties, collection) => ProjectInstance.FromFile(
                path,
                new ProjectOptions
                {
                    ProjectCollection = collection,
                    GlobalProperties = properties,
                }
            ),
            cancellationToken
        );

        return graph;
    }


    private sealed class DefaultFileSystem : MSBuildFileSystemBase;
}