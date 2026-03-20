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

        var cmd = new Command(
            "validate",
            "Validate a .mdz archive against the specification.\n\nExample:\n  mdz validate my-doc.mdz")
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
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        var result = MdzArchive.Validate(archivePath);
        var archiveName = Path.GetFileName(archivePath);

        foreach (var warning in result.Warnings)
            Console.WriteLine($"  Warning: {warning}");

        if (result.IsValid)
        {
            Console.WriteLine($"'{archiveName}' is valid.");
            return 0;
        }

        foreach (var error in result.Errors)
            Console.Error.WriteLine($"  {error}");

        Console.Error.WriteLine($"'{archiveName}' is INVALID ({result.Errors.Count} error(s)).");
        return 1;
    }
}
