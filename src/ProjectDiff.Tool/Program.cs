using Microsoft.Build.Locator;
using ProjectDiff.Tool;

MSBuildLocator.RegisterDefaults();

var tool = ProjectDiffTool.Create(new ExtendedConsole());

return await tool.InvokeAsync(args);