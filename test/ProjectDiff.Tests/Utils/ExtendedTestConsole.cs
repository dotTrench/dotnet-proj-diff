using System.CommandLine.IO;
using System.Text;
using ProjectDiff.Tool;

namespace ProjectDiff.Tests.Utils;

public class ExtendedTestConsole : IExtendedConsole
{
    private readonly TestWriter _out;
    private readonly TestWriter _error;

    public ExtendedTestConsole(string workingDirectory)
    {
        _out = new TestWriter();
        _error = new TestWriter();
        WorkingDirectory = workingDirectory;
    }

    public IStandardStreamWriter Out => _out;
    public bool IsOutputRedirected => false;
    public IStandardStreamWriter Error => _error;
    public bool IsErrorRedirected => false;
    public bool IsInputRedirected => false;
    public string WorkingDirectory { get; }

    public Stream OpenStandardOutput()
    {
        return _out.GetStream();
    }
}

public sealed class TestWriter : IStandardStreamWriter
{
    private readonly MemoryStream _stream = new();

    public void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        _stream.Write(Encoding.UTF8.GetBytes(value));
    }

    public Stream GetStream()
    {
        return _stream;
    }


    public string ReadToEnd()
    {
        return Encoding.UTF8.GetString(_stream.ToArray());
    }

    public override string ToString()
    {
        return ReadToEnd();
    }
}