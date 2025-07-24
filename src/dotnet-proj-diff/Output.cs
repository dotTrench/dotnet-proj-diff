namespace ProjectDiff.Tool;

public sealed class Output(FileInfo? outputFile, IConsole console)
{
    public string RootDirectory => outputFile?.DirectoryName ?? console.WorkingDirectory;

    public Stream Open()
    {
        return outputFile?.Create() ?? console.OpenStandardOutput();
    }

    public string NormalizePath(string path, bool absolutePaths) =>
        absolutePaths ? path.Replace('\\', '/') : Path.GetRelativePath(RootDirectory, path).Replace('\\', '/');
}
