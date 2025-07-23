using Microsoft.Build.Locator;
using ProjectDiff.Tool;

MSBuildLocator.RegisterDefaults();

var tool = ProjectDiffTool.BuildCli(new SystemConsole());

return await tool.InvokeAsync(args);