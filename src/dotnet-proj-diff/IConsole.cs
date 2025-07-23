namespace ProjectDiff.Tool;

public interface IConsole
{
    TextWriter Error { get; }
    TextWriter Out { get; }

    Stream OpenStandardOutput();
    string WorkingDirectory { get; }
}