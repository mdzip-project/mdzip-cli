using System.CommandLine;
using System.Reflection;
using System.Text;
using MDZip.Commands;

// Print UTF-8 (no BOM) everywhere. On Windows .NET otherwise uses the console's OEM code page
// (437 in the US) and replaces characters it lacks, corrupting `mdz ls`, `inspect` and error
// text (mdzip-cli#9). On Windows this also switches the console to code page 65001, so the
// original is put back on exit.
try
{
    var originalOutputEncoding = Console.OutputEncoding;
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        try { Console.OutputEncoding = originalOutputEncoding; }
        catch (IOException) { }
    };
}
catch (IOException)
{
    // No console to configure (e.g. a detached process); output still works.
}

var rawVersion = Assembly.GetEntryAssembly()?
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion
    ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
    ?? "unknown";
var version = rawVersion.Split('+')[0];

var rootCommand = new RootCommand(
    $"mdz - command-line tool for creating, extracting, validating, inspecting, and editing .mdz files. (v{version}) " +
    "Use 'mdz <command> --help' for command-specific options.")
{
    CreateCommand.Build(),
    AddCommand.Build(),
    RemoveCommand.Build(),
    ExtractCommand.Build(),
    ValidateCommand.Build(),
    LsCommand.Build(),
    InspectCommand.Build(),
    CatCommand.Build(),
    AssetsCommand.Build(),
    ManifestCommand.Build(),
    WorkspaceCommand.Build(),
    InfoCommand.Build(version),
};

if (args.Length == 0 || IsRootHelpRequest(args))
{
    MDZip.Cli.HelpPrinter.PrintRootHelp(rootCommand, version);
    return 0;
}

if (args.Length == 1 && args[0] == "-v")
    args = ["--version"];

return await rootCommand.InvokeAsync(args);

static bool IsRootHelpRequest(string[] args) =>
    args.Length == 1
    && (args[0] is "--help" or "-h" or "-?");
