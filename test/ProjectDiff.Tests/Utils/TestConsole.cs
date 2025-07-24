using System.IO.Pipelines;
using ProjectDiff.Tool;

namespace ProjectDiff.Tests.Utils;

public class TestConsole : IConsole
{
    private readonly MemoryStream _outStream = new MemoryStream();
    private readonly MemoryStream _errorStream = new MemoryStream();
    private readonly StreamWriter _out;
    private readonly StreamWriter _error;

    public TestConsole(string workingDirectory)
    {
        _out = new StreamWriter(_outStream);
        _error = new StreamWriter(_errorStream);
        WorkingDirectory = workingDirectory;
    }

    public string WorkingDirectory { get; }

    public TextWriter Error => _error;
    public TextWriter Out => _out;

    public string GetStandardOutput()
    {
        _out.Flush();
        _outStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_outStream);
        return reader.ReadToEnd();
    }

    public string GetStandardError()
    {
        _error.Flush();
        _errorStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_errorStream);
        return reader.ReadToEnd();
    }

    public Stream OpenStandardOutput()
    {
        var writer = PipeWriter.Create(_outStream);

        return writer.AsStream(leaveOpen: true);
    }
}
