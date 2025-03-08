using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace ProjectDiff.Tool;

public class ProjectDiffTool
{
    private readonly Parser _parser;
    private readonly IExtendedConsole _console;

    private ProjectDiffTool(Parser parser, IExtendedConsole console)
    {
        _parser = parser;
        _console = console;
    }


    public Task<int> InvokeAsync(string[] args)
    {
        return _parser.InvokeAsync(args, _console);
    }


    public static ProjectDiffTool Create(IExtendedConsole console)
    {
        var parser = BuildParser(console);
        return new ProjectDiffTool(parser, console);
    }

    private static Parser BuildParser(IExtendedConsole console)
    {
        return new CommandLineBuilder(new ProjectDiffCommand(console))
            .UseVersionOption()
            .UseHelp()
            .UseParseDirective()
            .UseParseErrorReporting()
            .UseExceptionHandler(
                (ex, ctx) =>
                {
                    var exitCode = ex switch
                    {
                        OperationCanceledException => 125,
                        AggregateException aggregate when aggregate.InnerExceptions.All(
                                i => i is OperationCanceledException
                            ) =>
                            125,
                        _ => 1
                    };
                    if (exitCode == 1)
                    {
                        ctx.Console.Error.Write(ex.ToString());
                    }

                    ctx.ExitCode = exitCode;
                }
            )
            .CancelOnProcessTermination()
            .Build();
    }
}