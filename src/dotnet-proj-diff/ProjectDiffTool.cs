using System.CommandLine;

namespace ProjectDiff.Tool;

public class ProjectDiffTool
{
    private readonly RootCommand _command;
    private readonly TextWriter? _stderr;
    private readonly TextWriter? _stdout;

    public ProjectDiffTool(RootCommand command, TextWriter? stderr, TextWriter? stdout)
    {
        _command = command;
        _stderr = stderr;
        _stdout = stdout;
    }

    public static ProjectDiffTool BuildCli(IConsole console, TextWriter? stderr = null, TextWriter? stdout = null)
    {
        var cli = new ProjectDiffCommand(console);


        return new ProjectDiffTool(cli, stderr, stdout);
    }

    public async Task<int> InvokeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        var parseResult = _command.Parse(args);

        var config = new InvocationConfiguration();
        if (_stderr is not null)
        {
            config.Error = _stderr;
        }

        if (_stdout is not null)
        {
            config.Output = _stdout;
        }

        return await parseResult.InvokeAsync(config, cancellationToken);
    }
}
