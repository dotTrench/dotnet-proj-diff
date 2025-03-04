using System.CommandLine.Parsing;
using Microsoft.Build.Locator;
using ProjectDiff.Tool;

MSBuildLocator.RegisterDefaults();

var cli = ProjectDiffTool.BuildParser();


return await cli.InvokeAsync(args, new ExtendedConsole());