using System.CommandLine;

namespace ProjectDiff.Tool;

public static class ProjectDiffTool
{
    public static CommandLineConfiguration BuildCli(IConsole console, TextWriter? stderr = null, TextWriter? stdout = null)
    {
        var cli = new CommandLineConfiguration(new ProjectDiffCommand(console));
        if (stderr is not null)
        {
            cli.Error = stderr;
        }

        if (stdout is not null)
        {
            cli.Output = stdout;
        }

        return cli;
    }
}
