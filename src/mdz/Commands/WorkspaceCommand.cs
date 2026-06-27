using System.CommandLine;
using System.CommandLine.Invocation;
using MDZip.Core;

namespace MDZip.Commands;

public static class WorkspaceCommand
{
    public static Command Build()
    {
        return new Command("workspace", "Inspect or export workspace data.")
        {
            BuildInspect(),
            BuildExportAsset(),
        };
    }

    private static Command BuildInspect()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var orphansOption = new Option<bool>("--orphans", "Include orphaned asset analysis.");
        var cmd = new Command("inspect", "Print workspace document/asset summary.") { archiveArg, orphansOption };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = HandleInspect(
                ctx.ParseResult.GetValueForArgument(archiveArg)!,
                ctx.ParseResult.GetValueForOption(orphansOption));
        });
        return cmd;
    }

    private static Command BuildExportAsset()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var assetArg = new Argument<string>("asset", "Archive-relative asset path.");
        var outputArg = new Argument<FileInfo>("output", "Output file path.");
        var cmd = new Command("export-asset", "Export one workspace asset.") { archiveArg, assetArg, outputArg };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = HandleExportAsset(
                ctx.ParseResult.GetValueForArgument(archiveArg)!,
                ctx.ParseResult.GetValueForArgument(assetArg),
                ctx.ParseResult.GetValueForArgument(outputArg)!);
        });
        return cmd;
    }

    private static int HandleInspect(FileInfo archive, bool orphans)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            var workspace = MdzArchive.OpenWorkspace(
                archivePath,
                new MdzOpenWorkspaceOptions { IncludeOrphanedAssetAnalysis = orphans });
            Console.WriteLine($"Title: {workspace.Title ?? "(untitled)"}");
            Console.WriteLine($"Mode: {workspace.Mode}");
            Console.WriteLine($"Entry point: {workspace.EntryPoint ?? "(none)"}");
            Console.WriteLine($"Documents: {workspace.Documents.Count}");
            foreach (var document in workspace.Documents)
                Console.WriteLine($"  {(document.IsEntryPoint ? "*" : "-")} {document.Path}  {document.Title}");
            Console.WriteLine($"Assets: {workspace.Assets.Count}");
            foreach (var asset in workspace.Assets)
                Console.WriteLine($"  - {asset.Path}  {asset.Kind}  {asset.ByteSize:N0} bytes");

            if (workspace.OrphanedAssets is not null)
                Console.WriteLine($"Orphaned image assets: {workspace.OrphanedAssets.OrphanedAssetPaths.Count}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int HandleExportAsset(FileInfo archive, string assetPath, FileInfo output)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            var workspace = MdzArchive.OpenWorkspace(archivePath);
            var asset = workspace.Assets.FirstOrDefault(item =>
                item.Path.Equals(assetPath, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                Console.Error.WriteLine($"Error: Asset '{assetPath}' was not found.");
                return 1;
            }

            MdzArchive.ExportWorkspaceAsset(asset, output.FullName);
            Console.WriteLine($"Exported '{asset.Path}' to '{output.FullName}'.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
