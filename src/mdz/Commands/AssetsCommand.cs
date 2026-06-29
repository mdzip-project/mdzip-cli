using System.CommandLine;
using System.CommandLine.Invocation;
using MDZip.Core;

namespace MDZip.Commands;

public static class AssetsCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var orphansOption = new Option<bool>("--orphans", "Show orphaned image asset analysis.");
        var allMarkdownOption = new Option<bool>("--all-markdown", "Scan all Markdown files for orphan analysis.");

        var cmd = new Command("assets", "Inspect archive assets.")
        {
            archiveArg,
            orphansOption,
            allMarkdownOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var orphans = ctx.ParseResult.GetValueForOption(orphansOption);
            var allMarkdown = ctx.ParseResult.GetValueForOption(allMarkdownOption);
            ctx.ExitCode = Handle(archive!, orphans, allMarkdown);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, bool orphans, bool allMarkdown)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            var entries = MdzArchive.ListDetailed(archivePath)
                .Where(entry => !entry.IsDirectory
                    && !entry.IsMarkdown
                    && !entry.Path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine("Assets:");
            foreach (var entry in entries)
            {
                var mime = MdzArchive.InferMimeType(entry.Path);
                Console.WriteLine($"  {entry.Path}  {entry.Size:N0} bytes  {MdzArchive.ClassifyAssetKind(entry.Path, mime)}  {mime}");
            }

            if (orphans)
            {
                var result = MdzArchive.FindOrphanedAssets(
                    archivePath,
                    new MdzOrphanedAssetsOptions { ScanMode = allMarkdown ? "all-markdown" : "entrypoint" });
                Console.WriteLine();
                Console.WriteLine("Orphaned image assets:");
                foreach (var path in result.OrphanedAssetPaths)
                    Console.WriteLine($"  {path}");
                if (result.OrphanedAssetPaths.Count == 0)
                    Console.WriteLine("  (none)");

                if (result.UnresolvedReferences.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Unresolved references:");
                    foreach (var issue in result.UnresolvedReferences)
                        Console.WriteLine($"  {issue.SourcePath}: {issue.Reference} ({issue.Reason})");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
