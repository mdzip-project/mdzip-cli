using System.CommandLine;
using System.CommandLine.Invocation;
using MDZip.Core;

namespace MDZip.Commands;

public static class CatCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var entryArg = new Argument<string>("entry", "Archive-relative entry path to read.");
        var base64Option = new Option<bool>("--base64", "Print raw base64.");
        var dataUriOption = new Option<bool>("--data-uri", "Print a data URI.");
        var mimeOption = new Option<string?>("--mime", "Fallback MIME type for --data-uri.");

        var cmd = new Command("cat", "Print an archive entry to stdout.")
        {
            archiveArg,
            entryArg,
            base64Option,
            dataUriOption,
            mimeOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var entry = ctx.ParseResult.GetValueForArgument(entryArg);
            var base64 = ctx.ParseResult.GetValueForOption(base64Option);
            var dataUri = ctx.ParseResult.GetValueForOption(dataUriOption);
            var mime = ctx.ParseResult.GetValueForOption(mimeOption);
            ctx.ExitCode = Handle(archive!, entry, base64, dataUri, mime);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, string entry, bool base64, bool dataUri, string? mime)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            if (dataUri)
                Console.WriteLine(MdzArchive.ReadDataUri(archivePath, entry, mime));
            else if (base64)
                Console.WriteLine(MdzArchive.ReadBase64(archivePath, entry));
            else
                Console.Write(MdzArchive.ReadText(archivePath, entry));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
