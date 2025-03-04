using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace ProjectDiff.Tool;

public static class ProjectDiffTool
{
    public static Parser BuildParser()
    {
        return new CommandLineBuilder(new ProjectDiffCommand())
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