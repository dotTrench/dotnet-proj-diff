namespace ProjectDiff.Tool;

public sealed class Output
{
    private readonly FileInfo? _outputFile;
    private readonly IConsole _console;

    public Output(FileInfo? outputFile, IConsole console)
    {
        _outputFile = outputFile;
        _console = console;
    }

    public string RootDirectory => _outputFile?.DirectoryName ?? _console.WorkingDirectory;

    public Stream Open() => _outputFile?.Create() ?? _console.OpenStandardOutput();

    public string NormalizePath(string path, bool absolutePaths) =>
        absolutePaths ? path.Replace('\\', '/') : Path.GetRelativePath(RootDirectory, path).Replace('\\', '/');
}
