using System.CommandLine;
using System.CommandLine.Invocation;
using Mdz.Core;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz add' subcommand.
/// Adds a file to an existing .mdz archive or replaces an existing entry.
/// </summary>
public static class AddCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to modify.");

        var entryPathArg = new Argument<string>(
            name: "entry-path",
            description: "Archive-relative path to add or replace (for example 'assets/logo.png').");

        var fileArg = new Argument<FileInfo>(
            name: "file",
            description: "Path to the local source file to add.");

        var cmd = new Command(
            "add",
            "Add or replace a file in a .mdz archive.\n\nExample:\n  mdz add my-doc.mdz assets/logo.png ./logo.png")
        {
            archiveArg,
            entryPathArg,
            fileArg,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var entryPath = ctx.ParseResult.GetValueForArgument(entryPathArg);
            var file = ctx.ParseResult.GetValueForArgument(fileArg);
            ctx.ExitCode = Handle(archive!, entryPath!, file!);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, string entryPath, FileInfo file)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        if (!file.Exists)
        {
            Console.Error.WriteLine($"Error: Source file '{file.FullName}' does not exist.");
            return 1;
        }

        try
        {
            MdzArchive.AddFile(archivePath, entryPath, file.FullName);
            Console.WriteLine($"Added '{entryPath}' to '{archivePath}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
