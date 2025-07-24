using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ProjectDiff.Core;

public static class BuildGraphDiff
{
    public static IEnumerable<DiffProject> Diff(
        BuildGraph previous,
        BuildGraph current,
        IEnumerable<string> modifiedFiles,
        ILoggerFactory? loggerFactory = null
    )
    {
        loggerFactory ??= NullLoggerFactory.Instance;
        var instance = new GraphDiffInstance(current, loggerFactory.CreateLogger<GraphDiffInstance>());

        return instance.Execute(previous, modifiedFiles.ToFrozenSet());
    }

    private sealed class GraphDiffInstance
    {
        private readonly BuildGraph _graph;
        private readonly Dictionary<string, bool> _modifiedProjects;
        private readonly ILogger<GraphDiffInstance> _logger;

        public GraphDiffInstance(BuildGraph graph, ILogger<GraphDiffInstance> logger)
        {
            _modifiedProjects = new Dictionary<string, bool>(graph.Projects.Count);
            _graph = graph;
            _logger = logger;
        }

        public IEnumerable<DiffProject> Execute(BuildGraph previous, FrozenSet<string> modifiedFiles)
        {
            foreach (var currentProject in _graph.Projects)
            {
                using var scope = _logger.BeginScope(currentProject.FullPath);
                var previousProject = previous.Projects.FirstOrDefault(it => it.FullPath == currentProject.FullPath);
                if (previousProject is null)
                {
                    _logger.LogDebug("Project not found in previous graph, marking as added");
                    yield return new DiffProject
                    {
                        Path = currentProject.FullPath,
                        Status = DiffStatus.Added,
                        ReferencedProjects = currentProject.References,
                    };
                }
                else if (HasProjectChanged(previousProject, currentProject, modifiedFiles))
                {
                    _logger.LogDebug("Project has changed, marking as modified");
                    yield return new DiffProject
                    {
                        Path = currentProject.FullPath,
                        Status = DiffStatus.Modified,
                        ReferencedProjects = currentProject.References,
                    };
                }
                else if (HasProjectReferencesChanged(previousProject, currentProject, modifiedFiles))
                {
                    _logger.LogDebug("Project references have changed, marking as reference changed" );
                    yield return new DiffProject
                    {
                        Path = currentProject.FullPath,
                        Status = DiffStatus.ReferenceChanged,
                        ReferencedProjects = currentProject.References,
                    };
                }
            }

            foreach (var previousProject in previous.Projects)
            {
                var existsInCurrent = _graph.Projects.Any(it => it.FullPath == previousProject.FullPath);
                if (!existsInCurrent)
                {
                    _logger.LogDebug("Project {Path} not found in current graph, marking as removed", previousProject.FullPath);
                    yield return new DiffProject
                    {
                        Path = previousProject.FullPath,
                        Status = DiffStatus.Removed,
                        ReferencedProjects = [],
                    };
                }
            }
        }

        private bool HasProjectChanged(
            BuildGraphProject previous,
            BuildGraphProject current,
            FrozenSet<string> modifiedFiles
        )
        {
            if (_modifiedProjects.TryGetValue(current.FullPath, out var isModified))
            {
                return isModified;
            }

            if (HasProjectInputFilesChanged(previous.InputFiles, current.InputFiles, modifiedFiles))
            {
                _modifiedProjects.Add(current.FullPath, true);
                return true;
            }

            _modifiedProjects.Add(current.FullPath, false);
            return false;
        }

        private bool HasProjectInputFilesChanged(
            IReadOnlyCollection<string> previous,
            IReadOnlyCollection<string> current,
            FrozenSet<string> modifiedFiles
        )
        {
            if (previous.Count != current.Count)
            {
                _logger.LogDebug(
                    "Input files count changed: {PreviousCount} -> {CurrentCount}",
                    previous.Count,
                    current.Count
                );
                return true;
            }

            foreach (var file in current)
            {
                if (!previous.Contains(file))
                {
                    _logger.LogInformation("Input file {File} added", file);
                    return true;
                }

                if (modifiedFiles.Contains(file))
                {
                    _logger.LogInformation("Input file {File} modified", file);
                    return true;
                }
            }

            foreach (var file in previous)
            {
                if (!current.Contains(file))
                {
                    _logger.LogInformation("Input file {File} removed", file);
                    return true;
                }
            }

            return false;
        }


        private bool HasProjectReferencesChanged(
            BuildGraphProject previous,
            BuildGraphProject current,
            FrozenSet<string> modifiedFiles
        )
        {
            if (previous.References.Count != current.References.Count)
            {
                _logger.LogDebug(
                    "References count changed: {PreviousCount} -> {CurrentCount}",
                    previous.References.Count,
                    current.References.Count
                );
                return true;
            }

            foreach (var reference in current.References)
            {
                if (!previous.References.Contains(reference))
                {
                    _logger.LogInformation("Reference {Reference} added", reference);
                    return true;
                }

                var currentReference = _graph.Projects.First(it => it.FullPath == reference);
                var previousReference = _graph.Projects.First(it => it.FullPath == reference);

                if (HasProjectChanged(previousReference, currentReference, modifiedFiles))
                {
                    _logger.LogInformation("Referenced project {Reference} modified", reference);
                    return true;
                }
            }

            foreach (var reference in previous.References)
            {
                if (!current.References.Contains(reference))
                {
                    _logger.LogInformation("Referenced project {Reference} removed", reference);
                    return true;
                }
            }

            return false;
        }
    }
}