using System.CommandLine;
using System.CommandLine.Invocation;
using MDZip.Core;

namespace MDZip.Commands;

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

        var treeOption = new Option<bool>(
            aliases: ["--tree"],
            description: "Print an inferred archive path tree.");

        var cmd = new Command(
            "inspect",
            "Inspect metadata and manifest information of a .mdz archive.\n\nExample:\n  mdz inspect my-doc.mdz")
        {
            archiveArg,
            treeOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var tree = ctx.ParseResult.GetValueForOption(treeOption);
            ctx.ExitCode = Handle(archive!, tree);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, bool tree)
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
                PrintField("  Mode", manifest.Mode);
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
            var mode = MdzArchive.ResolveMode(archivePath);
            Console.WriteLine($"Resolved mode: {mode}");
            if (entryPoint is not null)
                Console.WriteLine($"Resolved entry point: {entryPoint}");
            else
                Console.WriteLine("Resolved entry point: (none - no unambiguous entry point found)");

            var entries = MdzArchive.List(archivePath);
            Console.WriteLine($"Total files: {entries.Count}");
            if (tree)
            {
                Console.WriteLine();
                Console.WriteLine("Tree:");
                PrintTree(MdzArchive.BuildPathTree(archivePath), string.Empty);
            }

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
        if (!string.IsNullOrWhiteSpace(value))
            Console.WriteLine($"{label}: {value}");
    }

    private static void PrintTree(IReadOnlyList<PathTreeNode> nodes, string prefix)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var isLast = i == nodes.Count - 1;
            var branch = isLast ? "\\-- " : "|-- ";
            var suffix = node.IsDirectory ? "/" : string.Empty;
            Console.WriteLine($"{prefix}{branch}{node.Name}{suffix}");
            if (node.Children.Count > 0)
                PrintTree(node.Children, prefix + (isLast ? "    " : "|   "));
        }
    }
}
