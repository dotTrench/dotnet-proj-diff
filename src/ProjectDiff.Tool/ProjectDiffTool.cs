using System.CommandLine;

namespace ProjectDiff.Tool;

public sealed class ProjectDiffTool
{
    private readonly CommandLineConfiguration _cli;
    private readonly IConsole _console;

    private ProjectDiffTool(CommandLineConfiguration cli, IConsole console)
    {
        _cli = cli;
        _console = console;
    }


    public Task<int> InvokeAsync(string[] args)
    {
        return _cli.InvokeAsync(args);
    }


    public static ProjectDiffTool Create(IConsole console)
    {
        var parser = BuildCli(console);
        return new ProjectDiffTool(parser, console);
    }

    private static CommandLineConfiguration BuildCli(IConsole console)
    {
        return new CommandLineConfiguration(new ProjectDiffCommand(console))
        {
            Error = console.Error,
            Output = console.Out,
        };
    }
}