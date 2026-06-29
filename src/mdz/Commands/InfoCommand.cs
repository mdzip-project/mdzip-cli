using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using MDZip.Core;

namespace MDZip.Commands;

public static class InfoCommand
{
    public static Command Build(string appVersion)
    {
        var cmd = new Command("info", "Print mdz, core, runtime, and OS information.");
        cmd.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = Handle(appVersion);
        });
        return cmd;
    }

    private static int Handle(string appVersion)
    {
        var coreVersion = typeof(MdzArchive).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+')[0]
            ?? typeof(MdzArchive).Assembly.GetName().Version?.ToString()
            ?? "unknown";

        Console.WriteLine($"mdz: {appVersion}");
        Console.WriteLine($"mdzip-core: {coreVersion}");
        Console.WriteLine($".NET: {Environment.Version}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        return 0;
    }
}
