using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography;
using System.Text;
using MDZip.Core;

namespace MDZip.Commands;

/// <summary>
/// <c>mdz textconv &lt;archive&gt;</c>: prints an archive as plain text for Git's textconv hook, so
/// <c>git diff</c> and <c>git log -p</c> show what changed inside an <c>.mdz</c> instead of
/// "Binary files differ" (mdzip-cli#13).
/// <para>
/// Each file becomes a section headed <c>===== path =====</c>: <c>manifest.json</c> first, then
/// every other file in path order, so sections line up between versions. Text files are printed in
/// full with LF line endings; binary files are one line with their size and SHA-256, so a changed
/// image still shows as a changed line. The format matches mdzip.org's
/// <c>scripts/mdz-textconv.mjs</c>, so diffs look the same with either.
/// </para>
/// <para>
/// It never fails a diff: a file that isn't a readable archive (a Git LFS pointer, a damaged file)
/// is printed as-is after a one-line note, with exit code 0.
/// </para>
/// </summary>
public static class TextconvCommand
{
    /// <summary>Where the text is written. Tests swap in a <see cref="MemoryStream"/>.</summary>
    public static Func<Stream> OpenStandardOutput { get; set; } = Console.OpenStandardOutput;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "md", "markdown", "json", "txt", "svg", "drawio", "css", "csv", "tsv",
        "html", "htm", "xml", "yml", "yaml", "js", "mjs", "ts", "toml", "ini", "mmd",
    };

    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var cmd = new Command("textconv",
            "Print an archive as plain text, for Git diffs. " +
            "Set up with: git config diff.mdz.textconv \"mdz textconv\" and '*.mdz diff=mdz' in .gitattributes.")
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
        // Git passes the exact (temporary) file name, so don't add .mdz to an extensionless path.
        var archivePath = archive.FullName;
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: '{archivePath}' does not exist.");
            return 1;
        }

        Console.Out.Flush();
        var stdout = OpenStandardOutput();
        stdout.Write(Render(archivePath));
        stdout.Flush();
        return 0;
    }

    /// <summary>The text Git diffs for <paramref name="archivePath"/>, as UTF-8 bytes.</summary>
    public static byte[] Render(string archivePath)
    {
        try
        {
            return Encoding.UTF8.GetBytes(RenderArchive(archivePath));
        }
        catch (Exception ex)
        {
            // Not a readable archive: show it as-is rather than failing the whole diff.
            var note = Encoding.UTF8.GetBytes($"[not a readable .mdz: {ex.Message}]\n");
            return [.. note, .. File.ReadAllBytes(archivePath)];
        }
    }

    private static string RenderArchive(string archivePath)
    {
        // manifest.json first, then everything else in path order. Culture-aware comparison, like
        // localeCompare in the mdzip.org script, so both list sections in the same order.
        var paths = MdzArchive.List(archivePath)
            .OrderBy(p => p == "manifest.json" ? 0 : 1)
            .ThenBy(p => p, StringComparer.InvariantCulture)
            .ToList();

        var output = new StringBuilder();
        foreach (var entryPath in paths)
        {
            var bytes = MdzArchive.ReadBytes(archivePath, entryPath);
            output.Append("===== ").Append(entryPath).Append(" =====\n");
            if (IsTextPath(entryPath))
            {
                var text = DecodeText(bytes).Replace("\r\n", "\n").Replace('\r', '\n');
                output.Append(text.EndsWith('\n') ? text : text + "\n");
            }
            else
            {
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                output.Append($"[binary, {bytes.Length} bytes, sha256 {hash}]\n");
            }
            output.Append('\n');
        }

        // No blank line after the last section, as in the mdzip.org script.
        if (output.Length > 0)
            output.Length -= 1;
        return output.ToString();
    }

    /// <summary>Whether an entry is text, by its extension (shared with <see cref="MergeCommand"/>).</summary>
    internal static bool IsTextPath(string entryPath)
    {
        var name = entryPath[(entryPath.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 && TextExtensions.Contains(name[(dot + 1)..]);
    }

    /// <summary>UTF-8 without a leading BOM; invalid bytes become U+FFFD, as in a browser.</summary>
    private static string DecodeText(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var start = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
    }
}
