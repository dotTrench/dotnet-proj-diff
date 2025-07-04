﻿using System.CommandLine;
using Microsoft.Build.Locator;
using ProjectDiff.Tool;

MSBuildLocator.RegisterDefaults();

var tool = ProjectDiffTool.Create(new SystemConsole());

return await tool.InvokeAsync(args);