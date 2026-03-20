using System.CommandLine;
using System.CommandLine.Invocation;
using Mdz.Core;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz inspect' subcommand.
/// Displays metadata and manifest information from a .mdz archive.
/// </summary>
public static class InspectCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to inspect.");

        var cmd = new Command(
            "inspect",
            "Inspect metadata and manifest information of a .mdz archive.\n\nExample:\n  mdz inspect my-doc.mdz")
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

        try
        {
            var archiveInfo = new FileInfo(archivePath);
            Console.WriteLine($"Archive: {archiveInfo.Name}");
            Console.WriteLine($"  File size: {archiveInfo.Length:N0} bytes");
            Console.WriteLine();

            var manifest = MdzArchive.ReadManifest(archivePath);

            if (manifest is null)
            {
                Console.WriteLine("  No manifest.json found.");
                Console.WriteLine("  Warning: Version metadata is unavailable.");
            }
            else
            {
                Console.WriteLine("Manifest:");
                PrintField("  Spec version (spec.version)", manifest.Spec?.Version ?? manifest.LegacyMdz);
                PrintField("  Title", manifest.Title);
                PrintField("  Entry point", manifest.EntryPoint);
                PrintField("  Language", manifest.Language);
                PrintField("  Document version", manifest.Version);
                PrintField("  Description", manifest.Description);
                PrintField("  License", manifest.License);
                PrintField("  Created", manifest.Created);
                PrintField("  Modified", manifest.Modified);
                PrintField("  Cover", manifest.Cover);

                if (manifest.Author is not null || manifest.Authors is { Count: > 0 })
                {
                    Console.WriteLine("  Author(s):");

                    if (manifest.Author is not null)
                    {
                        var display = manifest.Author.Name ?? "(unnamed)";
                        if (manifest.Author.Email is not null)
                            display += $" <{manifest.Author.Email}>";
                        Console.WriteLine($"    - {display}");
                    }

                    if (manifest.Authors is { Count: > 0 })
                    {
                        foreach (var a in manifest.Authors)
                        {
                            var display = a.Name ?? "(unnamed)";
                            if (a.Email is not null)
                                display += $" <{a.Email}>";
                            Console.WriteLine($"    - {display}");
                        }
                    }
                }

                if (manifest.Keywords is { Count: > 0 })
                    Console.WriteLine($"  Keywords: {string.Join(", ", manifest.Keywords)}");
            }

            Console.WriteLine();

            var entryPoint = MdzArchive.ResolveEntryPoint(archivePath);
            if (entryPoint is not null)
                Console.WriteLine($"Resolved entry point: {entryPoint}");
            else
                Console.WriteLine("Resolved entry point: (none - no unambiguous entry point found)");

            var entries = MdzArchive.List(archivePath);
            Console.WriteLine($"Total files: {entries.Count}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintField(string label, string? value)
    {
        if (value is not null)
            Console.WriteLine($"{label}: {value}");
    }
}
