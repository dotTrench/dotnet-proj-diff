namespace ProjectDiff.Tool;

public interface IConsole
{
    Stream OpenStandardOutput();
    string WorkingDirectory { get; }
}
