using LibGit2Sharp;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace ProjectDiff.Tests.Utils;

public sealed class TestRepository : IDisposable
{
    private static readonly Identity DefaultIdentity = new("Tester", "tester@example.com");
    private readonly Repository _repository;

    private TestRepository(string path)
    {
        _repository = new Repository(path);
    }


    public static async Task<SetupResult<T>> SetupAsync<T>(Func<TestRepository, Task<T>> setup)
    {
        var directory = Directory.CreateTempSubdirectory("dotnet-proj-diff-test");

        var repository = new TestRepository(Repository.Init(directory.FullName));
        var result = await setup(repository);
        repository.StageAndCommitAllChanges();
        return new SetupResult<T>(result, repository);
    }

    public static async Task<TestRepository> SetupAsync(Func<TestRepository, Task> setup)
    {
        var directory = Directory.CreateTempSubdirectory("dotnet-proj-diff-test");

        var repository = new TestRepository(Repository.Init(directory.FullName));
        await setup(repository);
        repository.StageAndCommitAllChanges();
        return repository;
    }


    public static TestRepository CreateEmpty()
    {
        var directory = Directory.CreateTempSubdirectory("dotnet-proj-diff-test");

        var repository = new TestRepository(Repository.Init(directory.FullName));
        repository.StageAndCommitAllChanges();
        return repository;
    }

    public Repository Repository => _repository;
    public Identity Identity { get; set; } = DefaultIdentity;
    public string WorkingDirectory => _repository.Info.WorkingDirectory;

    public Tree HeadTree => _repository.Head.Tip.Tree;

    public void DeleteDirectory(string path, bool recursive = false)
    {
        Directory.Delete(GetPath(path), recursive);
    }

    public Task WriteAllTextAsync(string file, string content)
    {
        return File.WriteAllTextAsync(GetPath(file), content);
    }


    public string GetPath(params string[] parts)
    {
        return Path.Join([WorkingDirectory, .. parts]);
    }

    public string GetPath(string relativePath)
    {
        return Path.Join(WorkingDirectory, relativePath);
    }

    public void DeleteFile(string file)
    {
        File.Delete(GetPath(file));
    }


    public void StageAllChanges()
    {
        Commands.Stage(_repository, "*");
    }

    public Commit Commit(string? message = null)
    {
        var author = new Signature(Identity, DateTimeOffset.UtcNow);

        message ??= Guid.NewGuid().ToString("N");
        return _repository.Commit(
            message,
            author,
            author
        );
    }

    public Commit StageAndCommitAllChanges()
    {
        StageAllChanges();

        return Commit();
    }

    public void Dispose()
    {
        var gitFiles = Directory.EnumerateFiles(_repository.Info.Path, "*", SearchOption.AllDirectories);
        foreach (var gitFile in gitFiles)
        {
            var currentAttributes = File.GetAttributes(gitFile);
            if (currentAttributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(gitFile, FileAttributes.Normal);
            }
        }

        _repository.Dispose();

        Directory.Delete(WorkingDirectory, true);
    }


    public static implicit operator Repository(TestRepository repository) => repository.Repository;


    public Branch Checkout(Branch branch)
    {
        return Commands.Checkout(_repository, branch);
    }

    public Branch Checkout(string name)
    {
        return Commands.Checkout(_repository, name);
    }

    public Branch CreateBranch(string name)
    {
        return _repository.CreateBranch(name);
    }

    public Branch CreateAndCheckoutBranch(string name)
    {
        var branch = _repository.CreateBranch(name, _repository.Head.Tip);

        return Commands.Checkout(_repository, branch);
    }

    public DirectoryInfo CreateDirectory(string name)
    {
        var path = GetPath(name);

        return Directory.CreateDirectory(path);
    }

    public async Task<string> UpdateSolutionAsync(string name, Action<SolutionModel> configure)
    {
        var path = GetPath(name);
        var model = await SolutionSerializers.SlnFileV12.OpenAsync(path, CancellationToken.None);

        configure(model);

        await SolutionSerializers.SlnFileV12.SaveAsync(path, model, CancellationToken.None);

        return path;
    }

    public async Task<string> CreateSolutionAsync(string name, Action<SolutionModel> configure)
    {
        var path = GetPath(name);
        var sln = new SolutionModel();
        configure(sln);
        await SolutionSerializers.SlnFileV12.SaveAsync(path, sln, CancellationToken.None);

        return path;
    }

    public string CreateProject(string name, Action<ProjectRootElement>? configure = null)
    {
        var path = GetPath(name);
        var root = ProjectRootElement.Create(path);
        root.Sdk = "Microsoft.NET.Sdk";
        configure?.Invoke(root);
        root.Save();

        return root.FullPath;
    }

    public void UpdateProject(string path, Action<ProjectRootElement> configure)
    {
        var p = GetPath(path);
        var root = ProjectRootElement.Open(p);
        if (root is null)
        {
            throw new ArgumentException("Could not open project root", nameof(path));
        }

        configure(root);
        root.Save(p);
    }
}
