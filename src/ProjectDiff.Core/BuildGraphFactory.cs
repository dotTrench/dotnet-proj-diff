using System.Diagnostics;
using LibGit2Sharp;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Prediction;
using Microsoft.Build.Prediction.Predictors;

namespace ProjectDiff.Core;

public static class BuildGraphFactory
{
    private static readonly IProjectGraphPredictor[] ProjectGraphPredictors = ProjectPredictors
        .AllProjectGraphPredictors
        // We create the build graph ourselves, we don't want referenced csproj files to be part of the input files
        .Where(it => it is not ProjectFileAndImportsGraphPredictor)
        .ToArray();

    public static BuildGraph CreateForProjectGraph(
        ProjectGraph graph,
        Repository repository,
        IReadOnlyCollection<FileInfo> ignoredFiles
    )
    {
        var executor = new ProjectGraphPredictionExecutor(
            ProjectGraphPredictors,
            ProjectPredictors.AllProjectPredictors
        );

        var collector = new BuildGraphPredictionCollector(graph, repository, ignoredFiles);

        executor.PredictInputsAndOutputs(graph, collector);

        return collector.Build();
    }

    private sealed class BuildGraphPredictionCollector : IProjectPredictionCollector
    {
        private readonly ProjectGraph _projectGraph;
        private readonly Repository _repository;
        private readonly IReadOnlyCollection<FileInfo> _ignoredFiles;
        private readonly Dictionary<string, BuildGraphProjectCollector> _collectors;

        public BuildGraphPredictionCollector(
            ProjectGraph projectGraph,
            Repository repository,
            IReadOnlyCollection<FileInfo> ignoredFiles
        )
        {
            _projectGraph = projectGraph;
            _repository = repository;
            _ignoredFiles = ignoredFiles;
            _collectors = new Dictionary<string, BuildGraphProjectCollector>(_projectGraph.ProjectNodes.Count);
            foreach (var node in _projectGraph.ProjectNodes)
            {
                _collectors.TryAdd(
                    node.ProjectInstance.FullPath,
                    new BuildGraphProjectCollector(node.ProjectInstance.FullPath)
                );
            }
        }


        public void AddInputFile(string path, ProjectInstance projectInstance, string predictorName)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path, projectInstance.Directory);
            }

            // Only include files that are part of this repository
            if (!path.StartsWith(_repository.Info.WorkingDirectory))
            {
                return;
            }

            // Ignore files that are in the ignored files list
            if (_ignoredFiles.Count > 0 && _ignoredFiles.Any(it => it.FullName == path))
            {
                return;
            }

            // Ignore any files that are ignored by .gitignore
            var relativePath = Path.GetRelativePath(_repository.Info.WorkingDirectory, path).Replace('\\', '/');
            if (_repository.Ignore.IsPathIgnored(relativePath))
            {
                return;
            }

            if (!_collectors.TryGetValue(projectInstance.FullPath, out var collector))
            {
                Debug.Fail($"Failed to get collector for project {projectInstance.FullPath}");
                return;
            }

            collector.AddInputFile(path);
        }

        public void AddInputDirectory(string path, ProjectInstance projectInstance, string predictorName)
        {
        }

        public void AddOutputFile(string path, ProjectInstance projectInstance, string predictorName)
        {
        }

        public void AddOutputDirectory(string path, ProjectInstance projectInstance, string predictorName)
        {
        }

        public BuildGraph Build()
        {
            var projects = BuildGraphProjects();

            return new BuildGraph
            {
                Projects = projects.OrderBy(it => it.References.Count).ToList(),
            };
        }


        private IEnumerable<BuildGraphProject> BuildGraphProjects()
        {
            foreach (var node in _projectGraph.ProjectNodes)
            {
                if (!_collectors.TryGetValue(node.ProjectInstance.FullPath, out var collector))
                {
                    Debug.Fail("Could not find collector");
                    continue;
                }

                var references = node.ProjectReferences.Select(it => it.ProjectInstance.FullPath);
                collector.AddReferences(references);
            }

            return _collectors.Values.Select(it => it.ToBuildGraphProject());
        }
    }

    private sealed class BuildGraphProjectCollector
    {
        private readonly string _projectPath;
        private readonly HashSet<string> _inputFiles = [];
        private readonly HashSet<string> _references = [];

        public BuildGraphProjectCollector(string projectPath)
        {
            _projectPath = projectPath;
        }

        public void AddInputFile(string path)
        {
            lock (_inputFiles)
            {
                _inputFiles.Add(path);
            }
        }

        public void AddReferences(IEnumerable<string> references)
        {
            foreach (var reference in references)
            {
                _references.Add(reference);
            }
        }

        public BuildGraphProject ToBuildGraphProject()
        {
            lock (_inputFiles)
            {
                return new BuildGraphProject(
                    _projectPath,
                    _inputFiles,
                    _references
                );
            }
        }
    }
}