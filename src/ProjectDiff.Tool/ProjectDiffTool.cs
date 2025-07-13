using System.CommandLine;

namespace ProjectDiff.Tool;

public sealed class ProjectDiffTool
{
    private readonly CommandLineConfiguration _cli;

    private ProjectDiffTool(CommandLineConfiguration cli)
    {
        _cli = cli;
    }


    public Task<int> InvokeAsync(string[] args)
    {
        return _cli.InvokeAsync(args);
    }


    public static ProjectDiffTool Create(IConsole console)
    {
        var parser = BuildCli(console);
        return new ProjectDiffTool(parser);
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