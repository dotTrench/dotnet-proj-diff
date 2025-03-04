using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Prediction;
using Microsoft.Build.Prediction.Predictors;

namespace ProjectDiff.Core;

public struct ItemPredictionFilterArgs
{
    public string Path { get; init; }
}

public static class BuildGraphFactory
{
    private static readonly IProjectGraphPredictor[] ProjectGraphPredictors = ProjectPredictors
        .AllProjectGraphPredictors
        .Where(it => it is not ProjectFileAndImportsGraphPredictor)
        .ToArray();


    public static BuildGraph CreateForProjectGraph(
        ProjectGraph graph,
        Func<string, bool> inputFileFilter
    )
    {
        var executor = new ProjectGraphPredictionExecutor(
            ProjectGraphPredictors,
            ProjectPredictors.AllProjectPredictors
        );

        var collector = new BuildGraphPredictionCollector(graph, inputFileFilter);

        executor.PredictInputsAndOutputs(graph, collector);

        return collector.Build();
    }

    private sealed class BuildGraphPredictionCollector : IProjectPredictionCollector
    {
        private readonly ProjectGraph _projectGraph;
        private readonly Func<string, bool> _inputFileFilter;
        private readonly Dictionary<string, BuildGraphProjectCollector> _collectors;

        public BuildGraphPredictionCollector(
            ProjectGraph projectGraph,
            Func<string, bool> inputFileFilter
        )
        {
            _projectGraph = projectGraph;
            _inputFileFilter = inputFileFilter;
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

            var matchesFilter = _inputFileFilter(path);
            if (!matchesFilter)
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
            var projects = BuildGraphProjects().DistinctBy(it => it.FullPath).ToList();

            return new BuildGraph
            {
                Projects = projects,
            };
        }


        private IEnumerable<BuildGraphProject> BuildGraphProjects()
        {
            foreach (var node in _projectGraph.ProjectNodesTopologicallySorted)
            {
                if (!_collectors.TryGetValue(node.ProjectInstance.FullPath, out var collector))
                {
                    Debug.Fail("Could not find collector");
                    continue;
                }

                foreach (var reference in node.ProjectReferences)
                {
                    collector.AddReference(reference.ProjectInstance.FullPath);
                }

                yield return collector.ToBuildGraphProject();
            }
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

        public void AddReference(string path)
        {
            lock (_references)
            {
                _references.Add(path);
            }
        }

        public BuildGraphProject ToBuildGraphProject()
        {
            lock (_inputFiles)
            {
                lock (_references)
                {
                    return new BuildGraphProject(
                        _projectPath,
                        _inputFiles.ToList(),
                        _references.ToList()
                    );
                }
            }
        }
    }
}