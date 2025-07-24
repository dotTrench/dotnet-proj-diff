using LibGit2Sharp;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Logging;
using ProjectDiff.Core.Entrypoints;

namespace ProjectDiff.Core;

public sealed class ProjectGraphFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ProjectGraphFactory> _logger;

    public ProjectGraphFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<ProjectGraphFactory>();
    }

    public async Task<ProjectGraph> BuildForGitTree(
        Repository repository,
        Tree tree,
        IEntrypointProvider entrypointProvider,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Building project graph for Git tree '{TreeId}'", tree.Sha);
        using var projectCollection = new ProjectCollection();

        var fs = new GitTreeFileSystem(
            repository,
            tree,
            projectCollection,
            [],
            _loggerFactory.CreateLogger<GitTreeFileSystem>()
        )
        {
            // Disable eager loading of projects during entrypoint discovery to prevent user accidentally loading projects
            EagerLoadProjects = false
        };
        var entrypoints = await entrypointProvider.GetEntrypoints(
            repository.Info.WorkingDirectory,
            fs,
            cancellationToken
        );

        // Enable eager loading to fix issue with ms build not using the provided file system to load imports
        fs.EagerLoadProjects = true;
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

    public async Task<ProjectGraph> BuildForWorkingDirectory(
        Repository repository,
        IEntrypointProvider solutionFile,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Building project graph for working directory");
        using var projectCollection = new ProjectCollection();

        var fs = new DefaultFileSystem();
        var entrypoints = (await solutionFile.GetEntrypoints(
            repository.Info.WorkingDirectory,
            fs,
            cancellationToken
        )).ToList();
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
