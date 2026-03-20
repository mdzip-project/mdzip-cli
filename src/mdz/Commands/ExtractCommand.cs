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

        var allowInvalidOption = new Option<bool>(
            aliases: ["--allow-invalid"],
            description: "Extract even if the archive fails validation checks.");

        var cmd = new Command(
            "extract",
            "Extract the contents of a .mdz archive.\n\nExample:\n  mdz extract my-doc.mdz --output ./extracted")
        {
            archiveArg,
            outputOption,
            allowInvalidOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var outputDir = ctx.ParseResult.GetValueForOption(outputOption);
            var allowInvalid = ctx.ParseResult.GetValueForOption(allowInvalidOption);
            ctx.ExitCode = Handle(archive!, outputDir, allowInvalid);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, DirectoryInfo? outputDir, bool allowInvalid)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        if (!allowInvalid)
        {
            var validation = MdzArchive.Validate(archivePath);
            if (!validation.IsValid)
            {
                Console.Error.WriteLine("Error: Archive is invalid. Use --allow-invalid to extract anyway.");
                foreach (var error in validation.Errors)
                    Console.Error.WriteLine($"  {error}");
                return 1;
            }
        }

        // Default output directory: archive name without extension
        var dest = outputDir?.FullName
            ?? Path.Combine(
                Path.GetDirectoryName(archivePath) ?? Directory.GetCurrentDirectory(),
                Path.GetFileNameWithoutExtension(archivePath));

        try
        {
            MdzArchive.Extract(archivePath, dest);
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
