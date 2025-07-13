namespace ProjectDiff.Tool;

public sealed class SystemConsole : IConsole
{
    public TextWriter Error { get; } = Console.Error;
    public TextWriter Out { get; } = Console.Out;

    public Stream OpenStandardOutput() => Console.OpenStandardOutput();

    public string WorkingDirectory { get; } = Directory.GetCurrentDirectory();
}