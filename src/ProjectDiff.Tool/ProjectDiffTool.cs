using System.CommandLine;

namespace ProjectDiff.Tool;

public static class ProjectDiffTool
{
    public static CommandLineConfiguration BuildCli(IConsole console)
    {
        return new CommandLineConfiguration(new ProjectDiffCommand(console))
        {
            Error = console.Error,
            Output = console.Out,
        };
    }
}