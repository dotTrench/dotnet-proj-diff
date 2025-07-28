using System.IO.Pipelines;
using System.Text;
using ProjectDiff.Tool;

namespace ProjectDiff.Tests.Utils;

public class TestConsole : IConsole
{
    private readonly MemoryStream _outStream = new MemoryStream();

    public TestConsole(string workingDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    public string WorkingDirectory { get; }

    public string GetStandardOutput()
    {
        return Encoding.UTF8.GetString(_outStream.ToArray());
    }

    public Stream OpenStandardOutput()
    {
        var writer = PipeWriter.Create(_outStream);

        return writer.AsStream(leaveOpen: true);
    }
}
