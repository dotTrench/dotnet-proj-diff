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


    public static BuildGraph CreateForProjectGraph(ProjectGraph graph)
    {
        var executor = new ProjectGraphPredictionExecutor(
            ProjectGraphPredictors,
            ProjectPredictors.AllProjectPredictors
        );

        var collector = new BuildGraphPredictionCollector(graph);

        executor.PredictInputsAndOutputs(graph, collector);

        return collector.Build();
    }

    private sealed class BuildGraphPredictionCollector : IProjectPredictionCollector
    {
        private readonly ProjectGraph _projectGraph;
        private readonly Dictionary<string, BuildGraphProjectCollector> _collectors;

        public BuildGraphPredictionCollector(
            ProjectGraph projectGraph
        )
        {
            _projectGraph = projectGraph;
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

            if (!_collectors.TryGetValue(projectInstance.FullPath, out var collector))
            {
                Debug.Fail($"Failed to get collector for project {projectInstance.FullPath}");
                return;
            }

            var directoryPackagesPropsPath = projectInstance.GetPropertyValue("DirectoryPackagesPropsPath");

            if (directoryPackagesPropsPath == path)
                return;


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
                Projects = projects.ToList(),
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

                var references = node.ProjectReferences.Select(it => it.ProjectInstance.FullPath);
                var packageReferences = GetPackageReferences(node.ProjectInstance);
                yield return collector.ToBuildGraphProject(references, packageReferences);
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> GetPackageReferences(ProjectInstance project)
        {
            if (!IsPackageReferenceProject(project))
            {
                return [];
            }


            var centralPackageVersionsEnabled =
                project.GetPropertyValue("CentralPackageVersionsFileImported") == "true" &&
                project.GetPropertyValue("ManagePackageVersionsCentrally") == "true";


            if (!project.Build(
                    [CollectPackageReferences, CollectCentralPackageVersions],
                    [],
                    out var targetOutputs
                ))
            {
                Debug.Fail(
                    "Failed to collect package references",
                    new AggregateException(targetOutputs.Select(it => it.Value.Exception)).ToString()
                );
                return [];
            }

            // Find the first target output that matches `CollectPackageReferences`
            var matchingTargetOutputReference = targetOutputs.First(
                e => e.Key.Equals(CollectPackageReferences, StringComparison.OrdinalIgnoreCase)
            );

            var referenceItems = matchingTargetOutputReference.Value.Items;


            // Target that matches `CollectCentralPackageVersions`. This will be used to get the versions of `GlobalPackageReference` packages
            var matchingTargetOutputVersion = targetOutputs.First(
                e => e.Key.Equals(CollectCentralPackageVersions, StringComparison.OrdinalIgnoreCase)
            );

            var versionItems = matchingTargetOutputVersion.Value.Items;


            var centralPackageVersionOverrideDisabled =
                project.GetPropertyValue("CentralPackageVersionOverrideEnabled") == "false";
            // Transform each item into an InstalledPackageReference
            return referenceItems.Select(
                p =>
                {
                    if (!centralPackageVersionsEnabled)
                    {
                        return new KeyValuePair<string, string>(p.ItemSpec, p.GetMetadata("Version"));
                    }


                    var versionOverride = p.GetMetadata("VersionOverride");

                    if (!centralPackageVersionOverrideDisabled && !string.IsNullOrEmpty(versionOverride))
                    {
                        return new KeyValuePair<string, string>(p.ItemSpec, versionOverride);
                    }

                    // Find the matching version item for the current reference item
                    var versionItem = versionItems.FirstOrDefault(
                        v =>
                            v.ItemSpec.Equals(p.ItemSpec, StringComparison.OrdinalIgnoreCase)
                    );

                    if (versionItem is not null)
                    {
                        return new KeyValuePair<string, string>(p.ItemSpec, versionItem.GetMetadata("Version"));
                    }

                    return new KeyValuePair<string, string>(p.ItemSpec, p.GetMetadata("Version"));
                }
            );
        }

        private const string PackageReferenceTypeTag = "PackageReference";
        private const string PACKAGE_VERSION_TYPE_TAG = "PackageVersion";
        private const string VERSION_TAG = "Version";
        private const string FRAMEWORK_TAG = "TargetFramework";
        private const string FRAMEWORKS_TAG = "TargetFrameworks";
        private const string RestoreStyleTag = "RestoreProjectStyle";
        private const string NugetStyleTag = "NuGetProjectStyle";
        private const string AssetsFilePathTag = "ProjectAssetsFile";
        private const string IncludeAssets = "IncludeAssets";
        private const string PrivateAssets = "PrivateAssets";
        private const string CollectPackageReferences = "CollectPackageReferences";
        private const string CollectCentralPackageVersions = "CollectCentralPackageVersions";

        private static bool IsPackageReferenceProject(ProjectInstance project)
        {
            return project.GetPropertyValue(RestoreStyleTag) == "PackageReference" ||
                   project.GetItems(PackageReferenceTypeTag).Count != 0 ||
                   project.GetPropertyValue(NugetStyleTag) == "PackageReference" ||
                   project.GetPropertyValue(AssetsFilePathTag) != "";
        }
    }

    private sealed class BuildGraphProjectCollector
    {
        private readonly string _projectPath;
        private readonly HashSet<string> _inputFiles = [];

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

        public BuildGraphProject ToBuildGraphProject(
            IEnumerable<string> references,
            IEnumerable<KeyValuePair<string, string>> packageReferences
        )
        {
            lock (_inputFiles)
            {
                return new BuildGraphProject(
                    _projectPath,
                    _inputFiles.ToList(),
                    references.ToHashSet(),
                    packageReferences.Select(
                        it => new BuildGraphProjectPackageReference
                        {
                            Name = it.Key,
                            Version = it.Value
                        }
                    ).ToList()
                );
            }
        }
    }
}