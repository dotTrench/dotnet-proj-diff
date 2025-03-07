using System.Collections.Frozen;

namespace ProjectDiff.Core;

public static class BuildGraphDiff
{
    public static IEnumerable<DiffProject> Diff(
        BuildGraph previous,
        BuildGraph current,
        IEnumerable<string> modifiedFiles
    )
    {
        var instance = new GraphDiffInstance(current);

        return instance.Execute(previous, modifiedFiles.ToFrozenSet());
    }
}

internal sealed class GraphDiffInstance
{
    private readonly BuildGraph _graph;
    private readonly Dictionary<string, bool> _modifiedProjects;

    public GraphDiffInstance(BuildGraph graph)
    {
        _modifiedProjects = new Dictionary<string, bool>(graph.Projects.Count);
        _graph = graph;
    }

    public IEnumerable<DiffProject> Execute(BuildGraph previous, FrozenSet<string> modifiedFiles)
    {
        foreach (var currentProject in _graph.Projects)
        {
            var previousProject = previous.Projects.FirstOrDefault(it => it.Matches(currentProject));
            if (previousProject is null)
            {
                yield return new DiffProject
                {
                    Path = currentProject.FullPath,
                    Status = DiffStatus.Added,
                };
            }
            else if (HasProjectChanged(previousProject, currentProject, modifiedFiles))
            {
                yield return new DiffProject
                {
                    Path = currentProject.FullPath,
                    Status = DiffStatus.Modified,
                };
            }
            else if (HasProjectReferencesChanged(previousProject, currentProject, modifiedFiles))
            {
                yield return new DiffProject
                {
                    Path = currentProject.FullPath,
                    Status = DiffStatus.ReferenceChanged
                };
            }
        }

        foreach (var previousProject in previous.Projects)
        {
            var existsInCurrent = _graph.Projects.Any(it => it.Matches(previousProject));
            if (!existsInCurrent)
            {
                yield return new DiffProject
                {
                    Path = previousProject.FullPath,
                    Status = DiffStatus.Removed,
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

        if (HasProjectPackageReferencesChanged(previous.PackageReferences, current.PackageReferences))
        {
            _modifiedProjects.Add(current.FullPath, true);
            return true;
        }

        _modifiedProjects.Add(current.FullPath, false);
        return false;
    }

    private static bool HasProjectPackageReferencesChanged(
        IReadOnlyCollection<BuildGraphProjectPackageReference> previous,
        IReadOnlyCollection<BuildGraphProjectPackageReference> current
    )
    {
        if (previous.Count != current.Count)
        {
            return true;
        }

        foreach (var packageReference in current)
        {
            var prevRef = previous.FirstOrDefault(it => it.Name == packageReference.Name);
            if (prevRef is null)
            {
                return true;
            }

            if (prevRef.Version != packageReference.Version)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasProjectInputFilesChanged(
        IReadOnlyCollection<string> previous,
        IReadOnlyCollection<string> current,
        FrozenSet<string> modifiedFiles
    )
    {
        if (previous.Count != current.Count)
        {
            return true;
        }

        foreach (var file in current)
        {
            if (!previous.Contains(file))
            {
                return true;
            }

            if (modifiedFiles.Contains(file))
            {
                return true;
            }
        }

        foreach (var file in previous)
        {
            if (!current.Contains(file))
            {
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
            return true;
        }

        foreach (var reference in current.References)
        {
            if (!previous.References.Contains(reference))
            {
                return true;
            }

            var currentReference = _graph.Projects.First(it => it.FullPath == reference);
            var previousReference = _graph.Projects.First(it => it.FullPath == reference);

            if (HasProjectChanged(previousReference, currentReference, modifiedFiles))
            {
                return true;
            }
        }

        foreach (var reference in previous.References)
        {
            if (!current.References.Contains(reference))
            {
                return true;
            }
        }

        return false;
    }
}