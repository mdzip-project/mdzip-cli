using System.CommandLine;
using System.Reflection;
using Mdz.Commands;

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
};

if (args.Length == 0 || IsRootHelpRequest(args))
{
    Mdz.Cli.HelpPrinter.PrintRootHelp(rootCommand, version);
    return 0;
}

if (args.Length == 1 && args[0] == "-v")
    args = ["--version"];

return await rootCommand.InvokeAsync(args);

static bool IsRootHelpRequest(string[] args) =>
    args.Length == 1
    && (args[0] is "--help" or "-h" or "-?");
