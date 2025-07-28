namespace ProjectDiff.Tool;

public sealed class SystemConsole : IConsole
{
    public Stream OpenStandardOutput() => Console.OpenStandardOutput();
    public string WorkingDirectory { get; } = Directory.GetCurrentDirectory();
}
