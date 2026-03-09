using System.CommandLine;
using Mdz.Commands;

var rootCommand = new RootCommand("mdz — command-line tool for creating, extracting, validating, and inspecting .mdz files.")
{
    CreateCommand.Build(),
    ExtractCommand.Build(),
    ValidateCommand.Build(),
    LsCommand.Build(),
    InspectCommand.Build(),
};

return await rootCommand.InvokeAsync(args);
