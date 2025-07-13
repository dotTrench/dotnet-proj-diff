using LibGit2Sharp;
using Microsoft.Build.Evaluation;
using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class GitTreeFileSystemTests
{
    [Fact]
    public async Task EnumerateDirectoriesReturnsDirectories()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir1");
                repo.CreateDirectory("subdir2");
                await repo.WriteFileAsync("subdir1/test.txt", "Hello, World!");
                await repo.WriteFileAsync("subdir2/another.txt", "Another file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var directories = fileSystem.EnumerateDirectories(
            repo.WorkingDirectory,
            "*",
            SearchOption.TopDirectoryOnly
        ).ToList();

        var expectedDirectories = new[]
        {
            Path.Combine(repo.WorkingDirectory, "subdir1"),
            Path.Combine(repo.WorkingDirectory, "subdir2")
        };
        
        Assert.Equivalent(expectedDirectories, directories);
    }
    
    [Fact]
    public async Task EnumerateDirectoriesInSubdirectoryReturnsSubdirectories()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                repo.CreateDirectory("subdir/nested");
                await repo.WriteFileAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteFileAsync("subdir/nested/nested.txt", "Nested file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var directories = fileSystem.EnumerateDirectories(
            Path.Combine(repo.WorkingDirectory, "subdir"),
            "*",
            SearchOption.TopDirectoryOnly
        ).ToList();

        var expectedDirectories = new[]
        {
            Path.Combine(repo.WorkingDirectory, "subdir", "nested")
        };
        
        Assert.Equivalent(expectedDirectories, directories);
    }
    
    [Fact]
    public async Task EnumerateDirectoriesWithSearchOptionAllDirectoriesReturnsAllDirectories()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir1");
                repo.CreateDirectory("subdir2");
                repo.CreateDirectory("subdir1/nested");
                repo.CreateDirectory("subdir2/another");
                await repo.WriteFileAsync("subdir1/test.txt", "Hello, World!");
                await repo.WriteFileAsync("subdir2/another/another.txt", "Another file");
                await repo.WriteFileAsync("subdir1/nested/nested.txt", "Nested file");
                await repo.WriteFileAsync("test.txt", "Root file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var directories = fileSystem.EnumerateDirectories(
            repo.WorkingDirectory,
            "*",
            SearchOption.AllDirectories
        ).ToList();

        string[] expectedDirectories = [
            Path.Combine(repo.WorkingDirectory, "subdir1"),
            Path.Combine(repo.WorkingDirectory, "subdir1", "nested"),
            Path.Combine(repo.WorkingDirectory, "subdir2"),
            Path.Combine(repo.WorkingDirectory, "subdir2", "another")
        ];
        
        Assert.Equivalent(expectedDirectories, directories);
    }
    
    [Fact]
    public void EnumerateFilesInEmptyDirectoryReturnsEmpty()
    {
        using var repo = TestRepository.CreateEmpty();

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var files = fileSystem.EnumerateFiles(
            repo.WorkingDirectory,
            "*",
            SearchOption.TopDirectoryOnly
        );

        Assert.Empty(files);
    }

    [Fact]
    public async Task EnumerateFilesInDirectoryWithFileReturnsFile()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                await repo.WriteFileAsync("test.txt", "Hello, World!");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var files = fileSystem.EnumerateFiles(
            repo.WorkingDirectory,
            "*",
            SearchOption.TopDirectoryOnly
        );

        var file = Assert.Single(files);

        Assert.Equal(
            Path.Combine(repo.WorkingDirectory, "test.txt"),
            file
        );
    }

    [Fact]
    public async Task EnumerateFilesInDirectoryWithSubdirectoryReturnsFiles()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteFileAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteFileAsync("subdir/another.txt", "Another file");

                // Should not be included
                await repo.WriteFileAsync("test.txt", "Root file");
                
                repo.CreateDirectory("subdir/nested");
                await repo.WriteFileAsync("subdir/nested/nested.txt", "Nested file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var files = fileSystem.EnumerateFiles(
            Path.Combine(repo.WorkingDirectory, "subdir"),
            "*",
            SearchOption.TopDirectoryOnly
        ).ToList();

        string[] expectedFiles =
        [
            Path.Combine(repo.WorkingDirectory, "subdir", "test.txt"),
            Path.Combine(repo.WorkingDirectory, "subdir", "another.txt")
        ];

        Assert.Equivalent(expectedFiles, files);
    }

    [Fact]
    public async Task EnumerateFilesWithSearchOptionAllDirectoriesReturnsAllFiles()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteFileAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteFileAsync("subdir/another.txt", "Another file");
                await repo.WriteFileAsync("test.txt", "Root file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var files = fileSystem.EnumerateFiles(
            repo.WorkingDirectory,
            "*",
            SearchOption.AllDirectories
        );

        string[] expectedFiles =
        [
            Path.Combine(repo.WorkingDirectory, "subdir", "test.txt"),
            Path.Combine(repo.WorkingDirectory, "subdir", "another.txt"),
            Path.Combine(repo.WorkingDirectory, "test.txt")
        ];

        
        Assert.Equivalent(expectedFiles, files);
    }


    [Fact]
    public async Task EnumerateFilesInSubdirectoryWithSearchOptionAllDirectoriesReturnsFilesInSubdirectory()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteFileAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteFileAsync("subdir/another.txt", "Another file");
                await repo.WriteFileAsync("test.txt", "Root file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            []
        );

        var files = fileSystem.EnumerateFiles(
            Path.Combine(repo.WorkingDirectory, "subdir"),
            "*",
            SearchOption.AllDirectories
        );

        string[] expectedFiles =
        [
            Path.Combine(repo.WorkingDirectory, "subdir", "test.txt"),
            Path.Combine(repo.WorkingDirectory, "subdir", "another.txt"),
        ];

        Assert.Equivalent(expectedFiles, files);
    }
}