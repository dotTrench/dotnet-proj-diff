using System.Text;
using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectDiff.Core;
using ProjectDiff.Tests.Utils;

namespace ProjectDiff.Tests.Core;

public sealed class GitTreeFileSystemTests
{
    [Fact]
    public async Task ReadFile_ReturnsCorrectContent()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            await repo.WriteAllTextAsync("test.txt", "Hello, World!")
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        using var reader = fileSystem.ReadFile(Path.Combine(repo.WorkingDirectory, "test.txt"));
        var content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public async Task ReadFileAllText_ReturnsCorrectContent()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            await repo.WriteAllTextAsync("test.txt", "Hello, World!")
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var content = fileSystem.ReadFileAllText(Path.Combine(repo.WorkingDirectory, "test.txt"));

        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public async Task ReadFileAllBytes_ReturnsCorrectContent()
    {
        var expectedBytes = "Hello, World!"u8.ToArray();

        using var repo = await TestRepository.SetupAsync(async repo =>
            await repo.WriteAllTextAsync("test.txt", "Hello, World!")
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var bytes = fileSystem.ReadFileAllBytes(Path.Combine(repo.WorkingDirectory, "test.txt"));

        Assert.Equal(expectedBytes, bytes);
    }

    [Fact]
    public async Task DirectoryExists_ReturnsTrueForExistingDirectory()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteAllTextAsync("subdir/.gitkeep", "");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.DirectoryExists(Path.Combine(repo.WorkingDirectory, "subdir"));

        Assert.True(exists);
    }

    [Fact]
    public async Task DirectoryExists_ReturnsFalseForNonExistingDirectory()
    {
        using var repo = await TestRepository.SetupAsync(_ => Task.CompletedTask);

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.DirectoryExists(Path.Combine(repo.WorkingDirectory, "nonexistent"));

        Assert.False(exists);
    }

    [Fact]
    public async Task FileExists_ReturnsTrueForExistingFile()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            await repo.WriteAllTextAsync("test.txt", "Hello, World!")
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.FileExists(Path.Combine(repo.WorkingDirectory, "test.txt"));

        Assert.True(exists);
    }

    [Fact]
    public async Task FileExists_ReturnsFalseForNonExistingFile()
    {
        using var repo = await TestRepository.SetupAsync(_ => Task.CompletedTask);

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.FileExists(Path.Combine(repo.WorkingDirectory, "nonexistent.txt"));

        Assert.False(exists);
    }

    [Fact]
    public async Task FileOrDirectoryExists_ReturnsTrueForExistingFile()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            await repo.WriteAllTextAsync("test.txt", "Hello, World!")
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.FileOrDirectoryExists(Path.Combine(repo.WorkingDirectory, "test.txt"));

        Assert.True(exists);
    }

    [Fact]
    public async Task FileOrDirectoryExists_ReturnsTrueForExistingDirectory()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteAllTextAsync("subdir/.gitkeep", "");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.FileOrDirectoryExists(Path.Combine(repo.WorkingDirectory, "subdir"));

        Assert.True(exists);
    }

    [Fact]
    public async Task FileOrDirectoryExists_ReturnsFalseForNonExistingPath()
    {
        using var repo = await TestRepository.SetupAsync(_ => Task.CompletedTask);

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var exists = fileSystem.FileOrDirectoryExists(Path.Combine(repo.WorkingDirectory, "nonexistent"));

        Assert.False(exists);
    }

    [Fact]
    public async Task EnumerateDirectories_ReturnsDirectories()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir1");
                repo.CreateDirectory("subdir2");
                await repo.WriteAllTextAsync("subdir1/test.txt", "Hello, World!");
                await repo.WriteAllTextAsync("subdir2/another.txt", "Another file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
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
    public async Task EnumerateDirectories_InSubdirectoryReturnsSubdirectories()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                repo.CreateDirectory("subdir/nested");
                await repo.WriteAllTextAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteAllTextAsync("subdir/nested/nested.txt", "Nested file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
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
    public async Task EnumerateDirectories_WithSearchOptionAllDirectoriesReturnsAllDirectories()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir1");
                repo.CreateDirectory("subdir2");
                repo.CreateDirectory("subdir1/nested");
                repo.CreateDirectory("subdir2/another");
                await repo.WriteAllTextAsync("subdir1/test.txt", "Hello, World!");
                await repo.WriteAllTextAsync("subdir2/another/another.txt", "Another file");
                await repo.WriteAllTextAsync("subdir1/nested/nested.txt", "Nested file");
                await repo.WriteAllTextAsync("test.txt", "Root file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var directories = fileSystem.EnumerateDirectories(
            repo.WorkingDirectory,
            "*",
            SearchOption.AllDirectories
        ).ToList();

        string[] expectedDirectories =
        [
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
            [],
            NullLogger<GitTreeFileSystem>.Instance
        );

        var files = fileSystem.EnumerateFiles(
            repo.WorkingDirectory,
            "*",
            SearchOption.TopDirectoryOnly
        );

        Assert.Empty(files);
    }

    [Fact]
    public async Task EnumerateFiles_InDirectoryWithFileReturnsFile()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                await repo.WriteAllTextAsync("test.txt", "Hello, World!");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
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
    public async Task EnumerateFiles_InDirectoryWithSubdirectoryReturnsFiles()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteAllTextAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteAllTextAsync("subdir/another.txt", "Another file");

                // Should not be included
                await repo.WriteAllTextAsync("test.txt", "Root file");

                repo.CreateDirectory("subdir/nested");
                await repo.WriteAllTextAsync("subdir/nested/nested.txt", "Nested file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
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
    public async Task EnumerateFiles_WithSearchOptionAllDirectoriesReturnsAllFiles()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteAllTextAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteAllTextAsync("subdir/another.txt", "Another file");
                await repo.WriteAllTextAsync("test.txt", "Root file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
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
    public async Task EnumerateFiles_InSubdirectoryWithSearchOptionAllDirectoriesReturnsFilesInSubdirectory()
    {
        using var repo = await TestRepository.SetupAsync(async repo =>
            {
                repo.CreateDirectory("subdir");
                await repo.WriteAllTextAsync("subdir/test.txt", "Hello, World!");
                await repo.WriteAllTextAsync("subdir/another.txt", "Another file");
                await repo.WriteAllTextAsync("test.txt", "Root file");
            }
        );

        using var projects = new ProjectCollection();
        var fileSystem = new GitTreeFileSystem(
            repo,
            repo.HeadTree,
            projects,
            [],
            NullLogger<GitTreeFileSystem>.Instance
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
