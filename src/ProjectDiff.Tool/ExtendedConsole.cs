namespace ProjectDiff.Tool;

public sealed class SystemConsole : IConsole
{
    public TextWriter Error { get; } = Console.Error;
    public TextWriter Out { get; } = Console.Out;

    public Stream OpenStandardOutput()
    {
        return Console.OpenStandardOutput();
    }

    public string WorkingDirectory { get; } = Directory.GetCurrentDirectory();
}

public interface IConsole
{
    TextWriter Error { get; }
    TextWriter Out { get; }

    Stream OpenStandardOutput();
    string WorkingDirectory { get; }
}