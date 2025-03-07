using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Prediction;
using Microsoft.Build.Prediction.Predictors;

namespace ProjectDiff.Core;

public static class BuildGraphFactory
{
    private static readonly IProjectGraphPredictor[] ProjectGraphPredictors = ProjectPredictors
        .AllProjectGraphPredictors
        .Where(it => it is not ProjectFileAndImportsGraphPredictor)
        .ToArray();


    public static BuildGraph CreateForProjectGraph(
        ProjectGraph graph,
        FrozenSet<string> changedFiles
    )
    {
        var executor = new ProjectGraphPredictionExecutor(
            ProjectGraphPredictors,
            ProjectPredictors.AllProjectPredictors
        );

        var collector = new BuildGraphPredictionCollector(graph, changedFiles);

        executor.PredictInputsAndOutputs(graph, collector);

        return collector.Build();
    }

    private sealed class BuildGraphPredictionCollector : IProjectPredictionCollector
    {
        private readonly ProjectGraph _projectGraph;
        private readonly Dictionary<string, BuildGraphProjectCollector> _collectors;
        private readonly FrozenSet<string> _changedFiles;

        public BuildGraphPredictionCollector(ProjectGraph projectGraph, FrozenSet<string> changedFiles)
        {
            _projectGraph = projectGraph;
            _changedFiles = changedFiles;
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

            if (!_changedFiles.Contains(path))
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