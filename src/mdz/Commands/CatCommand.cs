using System.CommandLine;
using System.CommandLine.Invocation;
using MDZip.Core;

namespace MDZip.Commands;

public static class CatCommand
{
    /// <summary>
    /// Where <c>cat</c> writes an entry's raw bytes. Tests swap in a <see cref="MemoryStream"/>,
    /// because <see cref="Console.SetOut"/> doesn't capture writes to the standard output stream.
    /// </summary>
    public static Func<Stream> OpenStandardOutput { get; set; } = Console.OpenStandardOutput;

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
                WriteRaw(MdzArchive.ReadBytes(archivePath, entry));
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            // Lookups are case-sensitive (§5.4.1); a case-only match is suggested, never opened.
            var near = MdzArchive.FindCaseOnlyMatch(archivePath, entry);
            var hint = near is null ? string.Empty : $" Did you mean '{near}'? Paths are case-sensitive.";
            Console.Error.WriteLine($"Error: {ex.Message}{hint}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Writes the entry exactly as stored: no decoding or re-encoding, so line endings, a BOM and
    /// non-UTF-8 text survive, and <c>mdz cat a.mdz x &gt; y</c> then <c>mdz add</c> round-trips
    /// (mdzip-cli#9).
    /// </summary>
    private static void WriteRaw(byte[] bytes)
    {
        Console.Out.Flush();
        var stdout = OpenStandardOutput();
        stdout.Write(bytes);
        stdout.Flush();
    }
}
