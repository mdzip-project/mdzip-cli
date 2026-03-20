using System.CommandLine;
using System.CommandLine.Invocation;
using Mdz.Core;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz remove' subcommand.
/// Removes a file entry from an existing .mdz archive.
/// </summary>
public static class RemoveCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to modify.");

        var entryPathArg = new Argument<string>(
            name: "entry-path",
            description: "Archive-relative path to remove (for example 'assets/logo.png').");

        var cmd = new Command(
            "remove",
            "Remove a file from a .mdz archive.\n\nExample:\n  mdz remove my-doc.mdz assets/logo.png")
        {
            archiveArg,
            entryPathArg,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var entryPath = ctx.ParseResult.GetValueForArgument(entryPathArg);
            ctx.ExitCode = Handle(archive!, entryPath!);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, string entryPath)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            MdzArchive.RemoveFile(archivePath, entryPath);
            Console.WriteLine($"Removed '{entryPath}' from '{archivePath}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
