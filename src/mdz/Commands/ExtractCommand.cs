using System.CommandLine;
using System.CommandLine.Invocation;
using Mdz.Core;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz extract' subcommand.
/// Extracts a .mdz archive to a destination directory.
/// </summary>
public static class ExtractCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to extract.");

        var outputOption = new Option<DirectoryInfo?>(
            aliases: ["--output", "-o"],
            description: "Destination directory. Defaults to a directory named after the archive in the current folder.");

        var cmd = new Command("extract", "Extract the contents of a .mdz archive.")
        {
            archiveArg,
            outputOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var outputDir = ctx.ParseResult.GetValueForOption(outputOption);
            ctx.ExitCode = Handle(archive!, outputDir);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, DirectoryInfo? outputDir)
    {
        if (!archive.Exists)
        {
            Console.Error.WriteLine($"Error: Archive '{archive.FullName}' does not exist.");
            return 1;
        }

        // Default output directory: archive name without extension
        var dest = outputDir?.FullName
            ?? Path.Combine(
                archive.DirectoryName ?? Directory.GetCurrentDirectory(),
                Path.GetFileNameWithoutExtension(archive.Name));

        try
        {
            MdzArchive.Extract(archive.FullName, dest);
            Console.WriteLine($"Extracted to '{dest}'");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
