using System.CommandLine;
using System.CommandLine.IO;

namespace ProjectDiff.Tool;

public sealed class ExtendedConsole : IExtendedConsole
{
    private readonly SystemConsole _console;

    public ExtendedConsole()
    {
        _console = new SystemConsole();
        WorkingDirectory = Directory.GetCurrentDirectory();
    }

    public IStandardStreamWriter Error => _console.Error;

    public bool IsErrorRedirected => _console.IsErrorRedirected;

    public IStandardStreamWriter Out => _console.Out;

    public bool IsOutputRedirected => _console.IsOutputRedirected;

    public bool IsInputRedirected => _console.IsInputRedirected;

    public string WorkingDirectory { get; }

    public Stream OpenStandardOutput()
    {
        return Console.OpenStandardOutput();
    }
}

public interface IExtendedConsole : IConsole
{
    Stream OpenStandardOutput();
    string WorkingDirectory { get; }
}