using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;

namespace ProjectDiff.Tests;

public class ModuleInit
{
    [ModuleInitializer]
    public static void Run()
    {
        MSBuildLocator.RegisterDefaults();
    }
}
