using ProjectDiff.Tests.Utils;
using ProjectDiff.Tool;

namespace ProjectDiff.Tests.Tool;

public sealed class ProjectDiffTests
{
    public static TheoryData<OutputFormat> OutputFormats => new(Enum.GetValues<OutputFormat>());

    public static TheoryData<OutputFormat> DirectoryScanOutputFormats => new(
        OutputFormats.Where(it => it.Data != OutputFormat.Slnf)
    );

    private static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Json => "json",
        OutputFormat.Plain => "txt",
        OutputFormat.Slnf => "json",
        OutputFormat.Traversal => "xml",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsAddedFiles(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var solution = await r.CreateSolutionAsync(
                    "Sample.sln",
                    sln => { sln.AddProject("Sample/Sample.csproj"); }
                );

                r.CreateDirectory("Sample");
                await r.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");
                r.CreateProject("Sample/Sample.csproj");

                return solution;
            }
        );
        var (sln, repo) = res;

        await repo.WriteAllTextAsync("Sample/MyClass2", "// Some other content");

        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }


    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsDeletedFiles(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var sln = await r.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");

                await r.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");

                return sln;
            }
        );
        var (sln, repo) = res;
        repo.DeleteFile("Sample/MyClass.cs");

        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }


    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsModifiedFiles(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var sln = await r.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");
                await r.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");

                return sln;
            }
        );
        var (sln, repo) = res;
        await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some new content");

        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }

    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsChangesInReferencedProjects(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var sln = await r.CreateSolutionAsync(
                    "Sample.sln",
                    sln =>
                    {
                        sln.AddProject("Sample/Sample.csproj");
                        sln.AddProject("Tests/Tests.csproj");
                    }
                );

                r.CreateDirectory("Sample");
                r.CreateDirectory("Tests");

                r.CreateProject("Sample/Sample.csproj");
                r.CreateProject(
                    "Tests/Tests.csproj",
                    p => p.AddItem("ProjectReference", @"..\Sample\Sample.csproj")
                );

                return sln;
            }
        );
        var (sln, repo) = res;

        await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some new content");

        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }


    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsChangesInNestedReferencedProjects(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var sln = await r.CreateSolutionAsync(
                    "Sample.sln",
                    sln =>
                    {
                        sln.AddProject("Sample/Sample.csproj");
                        sln.AddProject("Application/Application.csproj");
                        sln.AddProject("Tests/Tests.csproj");
                    }
                );

                r.CreateDirectory("Sample");
                r.CreateDirectory("Tests");
                r.CreateDirectory("Application");

                r.CreateProject("Sample/Sample.csproj");
                r.CreateProject(
                    "Application/Application.csproj",
                    p => p.AddItem("ProjectReference", @"..\Sample\Sample.csproj")
                );
                r.CreateProject(
                    "Tests/Tests.csproj",
                    p => p.AddItem("ProjectReference", @"..\Application\Application.csproj")
                );

                return sln;
            }
        );
        var (sln, repo) = res;

        await repo.WriteAllTextAsync("Sample/MyClass.cs", "// Some new content");

        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }

    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsDeletedProjectsWhenOptionIsSet(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                r.CreateDirectory("Sample");
                r.CreateDirectory("Tests");


                r.CreateProject("Sample/Sample.csproj");
                r.CreateProject(
                    "Tests/Tests.csproj",
                    p => p.AddItem("ProjectReference", @"..\Sample\Sample.csproj")
                );
                return await r.CreateSolutionAsync(
                    "Sample.sln",
                    sln =>
                    {
                        sln.AddProject("Sample/Sample.csproj");
                        sln.AddProject("Tests/Tests.csproj");
                    }
                );
            }
        );

        var (sln, repo) = res;
        repo.DeleteFile("Tests/Tests.csproj");
        await repo.UpdateSolutionAsync(
            "Sample.sln",
            x =>
            {
                var proj = x.FindProject("Tests/Tests.csproj");
                Assert.NotNull(proj);
                x.RemoveProject(proj);
            }
        );
        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}", "--include-deleted");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }


    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DoesNotDetectDeletedProjectsWhenOptionIsNotSet(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                r.CreateDirectory("Sample");
                r.CreateDirectory("Tests");


                r.CreateProject("Sample/Sample.csproj");
                r.CreateProject(
                    "Tests/Tests.csproj",
                    p => p.AddItem("ProjectReference", @"..\Sample\Sample.csproj")
                );
                return await r.CreateSolutionAsync(
                    "Sample.sln",
                    sln =>
                    {
                        sln.AddProject("Sample/Sample.csproj");
                        sln.AddProject("Tests/Tests.csproj");
                    }
                );
            }
        );

        var (sln, repo) = res;
        await repo.UpdateSolutionAsync(
            "Sample.sln",
            x =>
            {
                var proj = x.FindProject("Tests/Tests.csproj");
                Assert.NotNull(proj);
                x.RemoveProject(proj);
            }
        );
        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }


    [Theory]
    [MemberData(nameof(OutputFormats))]
    public async Task DetectsAddedProjects(OutputFormat format)
    {
        using var res = await TestRepository.SetupAsync(static async r =>
            {
                var sln = await r.CreateSolutionAsync("Sample.sln", sln => sln.AddProject("Sample/Sample.csproj"));
                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");
                await r.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");

                return sln;
            }
        );
        var (sln, repo) = res;

        await repo.UpdateSolutionAsync(
            "Sample.sln",
            x => x.AddProject("Added/Added.csproj")
        );

        repo.CreateDirectory("Added");
        repo.CreateProject("Added/Added.csproj");


        var output = await ExecuteAndReadStdout(repo, sln, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }

    [Theory]
    [MemberData(nameof(DirectoryScanOutputFormats))]
    public async Task DetectsAddedProjectsWithDirectoryScan(OutputFormat format)
    {
        using var repo = await TestRepository.SetupAsync(static async r =>
            {
                r.CreateDirectory("Sample");
                r.CreateProject("Sample/Sample.csproj");
                await r.WriteAllTextAsync("Sample/MyClass.cs", "// Some content");
            }
        );

        repo.CreateDirectory("Added");
        repo.CreateProject("Added/Added.csproj");


        var output = await ExecuteAndReadStdout(repo, $"--format={format}");

        await Verify(output, GetExtension(format))
            .UseParameters(format);
    }

    [Fact]
    public void BuildingCliIsValid()
    {
        var console = new TestConsole(Directory.GetCurrentDirectory());   
        var cli = ProjectDiffTool.BuildCli(console);
        cli.ThrowIfInvalid();
    }


    private static async Task<string> ExecuteAndReadStdout(
        TestRepository repository,
        params string[] args
    )
    {
        var console = new TestConsole(repository.WorkingDirectory);
        
        var cli = ProjectDiffTool.BuildCli(console);
        var exitCode = await cli.InvokeAsync(args.Append("--log-level=None").ToArray());
        if (exitCode != 0)
        {
            var stderr = console.GetStandardError();
            Assert.Fail($"Program exited with exit code {exitCode}: {stderr}");
        }

        return console.GetStandardOutput();
    }
}