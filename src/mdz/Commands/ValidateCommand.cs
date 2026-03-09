using System.CommandLine;
using System.CommandLine.Invocation;
using Mdz.Core;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz validate' subcommand.
/// Validates a .mdz archive against the specification.
/// </summary>
public static class ValidateCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to validate.");

        var cmd = new Command("validate", "Validate a .mdz archive against the specification.")
        {
            archiveArg,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            ctx.ExitCode = Handle(archive!);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive)
    {
        if (!archive.Exists)
        {
            Console.Error.WriteLine($"Error: Archive '{archive.FullName}' does not exist.");
            return 1;
        }

        var result = MdzArchive.Validate(archive.FullName);

        foreach (var warning in result.Warnings)
            Console.WriteLine($"  Warning: {warning}");

        if (result.IsValid)
        {
            Console.WriteLine($"'{archive.Name}' is valid.");
            return 0;
        }

        foreach (var error in result.Errors)
            Console.Error.WriteLine($"  {error}");

        Console.Error.WriteLine($"'{archive.Name}' is INVALID ({result.Errors.Count} error(s)).");
        return 1;
    }
}
