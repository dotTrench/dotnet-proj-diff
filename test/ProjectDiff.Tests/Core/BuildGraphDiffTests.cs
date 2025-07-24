using ProjectDiff.Core;

namespace ProjectDiff.Tests.Core;

public sealed class BuildGraphDiffTests
{
    [Fact]
    public void ReturnsAddedProjects()
    {
        var graph1 = new BuildGraph
        {
            Projects = []
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);


        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Added, project.Status);
    }

    [Fact]
    public void ReturnsRemovedProjects()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = []
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);

        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Removed, project.Status);
    }

    [Fact]
    public void ReturnsProjectsWithModifiedFiles()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample.cs"], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample.cs"], [])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, ["/path/to/project/sample.cs"]);

        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public void ReturnsProjectsWithAddedFiles()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample.cs"], [])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);

        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public void ReturnsProjectsWhenReferenceIsChanged()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project2.csproj"])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project3.csproj"])]
        };

        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.ReferenceChanged, project.Status);
    }

    [Fact]
    public void ReturnsProjectsWhenReferenceIsRemoved()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project2.csproj"])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };

        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.ReferenceChanged, project.Status);
    }

    [Fact]
    public void ReturnsProjectsWhenReferenceIsAdded()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project2.csproj"])]
        };

        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.ReferenceChanged, project.Status);
    }

    [Fact]
    public void ReturnsEmptyDiffWhenGraphsAreEqual()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };

        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        Assert.Empty(diff);
    }

    [Fact]
    public void ReturnsProjectWhenReferencedProjectIsModified()
    {
        var graph1 = new BuildGraph
        {
            Projects =
            [
                new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project2.csproj"]),
                new BuildGraphProject("/path/to/project2.csproj", ["file.cs"], [])
            ]
        };
        var graph2 = new BuildGraph
        {
            Projects =
            [
                new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project2.csproj"]),
                new BuildGraphProject("/path/to/project2.csproj", ["file.cs"], [])
            ],
        };

        var diff = BuildGraphDiff.Diff(graph1, graph2, ["file.cs"]);

        var expected = new[]
        {
            new DiffProject
            {
                Path = "/path/to/project1.csproj",
                Status = DiffStatus.ReferenceChanged,
                ReferencedProjects = ["/path/to/project2.csproj"],
            },
            new DiffProject
            {
                Path = "/path/to/project2.csproj",
                Status = DiffStatus.Modified,
                ReferencedProjects = [],
            }
        };
        Assert.Equivalent(expected, diff);
    }

    [Fact]
    public void ReturnsProjectWhenInputFileIsAdded()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample.cs"], [])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public void ReturnsProjectWhenInputFileIsRemoved()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample.cs"], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], [])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public void ReturnsProjectWhenInputFileIsChangedButNotModified()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample.cs"], [])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", ["/path/to/project/sample2.cs"], [])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.Modified, project.Status);
    }

    [Fact]
    public void ReturnsProjectWhenReferenceIsChangedButNotModified()
    {
        var graph1 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project2.csproj"])]
        };
        var graph2 = new BuildGraph
        {
            Projects = [new BuildGraphProject("/path/to/project1.csproj", [], ["/path/to/project3.csproj"])]
        };
        var diff = BuildGraphDiff.Diff(graph1, graph2, []);
        var project = Assert.Single(diff);
        Assert.Equal("/path/to/project1.csproj", project.Path);
        Assert.Equal(DiffStatus.ReferenceChanged, project.Status);
    }
}
