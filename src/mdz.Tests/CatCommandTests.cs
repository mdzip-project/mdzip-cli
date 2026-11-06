using System.CommandLine;
using System.Text;
using MDZip.Commands;
using MDZip.Core;

namespace MDZip.Tests;

/// <summary>
/// <c>mdz cat</c> writes an entry's bytes exactly as stored (mdzip-cli#9): no decoding, so
/// non-ASCII text, a BOM, CRLF and non-UTF-8 files come through untouched.
/// </summary>
[Collection("Console")]
public sealed class CatCommandTests : IDisposable
{
    private readonly string _tempDir;

    public CatCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-cat-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>Runs <c>mdz cat</c> and returns its exit code and the raw bytes it wrote.</summary>
    private static (int ExitCode, byte[] Bytes) Cat(string archive, string entry)
    {
        var original = CatCommand.OpenStandardOutput;
        var output = new MemoryStream();
        CatCommand.OpenStandardOutput = () => output;
        try
        {
            var exitCode = new RootCommand { CatCommand.Build() }
                .InvokeAsync(["cat", archive, entry]).GetAwaiter().GetResult();
            return (exitCode, output.ToArray());
        }
        finally
        {
            CatCommand.OpenStandardOutput = original;
        }
    }

    private string MakeArchive(params (string Path, byte[] Bytes)[] files)
    {
        var src = Path.Combine(_tempDir, "src-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var (path, bytes) in files)
        {
            var full = Path.Combine(src, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, bytes);
        }
        var archive = Path.Combine(_tempDir, Guid.NewGuid().ToString("N")[..8] + ".mdz");
        MdzArchive.Create(archive, src);
        return archive;
    }

    [Fact]
    public void Cat_NonAsciiMarkdown_WritesUtf8BytesUnchanged()
    {
        var text = Encoding.UTF8.GetBytes("# Café — test ✅\n\nÜber naïve 日本語\n");
        var archive = MakeArchive(("index.md", text));

        var (exitCode, bytes) = Cat(archive, "index.md");

        Assert.Equal(0, exitCode);
        Assert.Equal(text, bytes);
    }

    [Fact]
    public void Cat_TextWithBomAndCrlf_WritesStoredBytes()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var text = bom.Concat(Encoding.UTF8.GetBytes("line one — é\r\nline two ✅\r\n")).ToArray();
        var archive = MakeArchive(("index.md", "# Doc\n"u8.ToArray()), ("notes/readme.txt", text));

        var (exitCode, bytes) = Cat(archive, "notes/readme.txt");

        Assert.Equal(0, exitCode);
        Assert.Equal(MdzArchive.ReadBytes(archive, "notes/readme.txt"), bytes);
        Assert.Equal(bom, bytes[..3]);
        Assert.Contains((byte)'\r', bytes);
    }

    [Fact]
    public void Cat_NonUtf8Text_IsNotReplaced()
    {
        // "Café" in Latin-1: 0xE9 isn't valid UTF-8, so decoding would turn it into U+FFFD.
        byte[] latin1 = [0x43, 0x61, 0x66, 0xE9, 0x0A];
        var archive = MakeArchive(("index.md", "# Doc\n"u8.ToArray()), ("legacy.txt", latin1));

        var (exitCode, bytes) = Cat(archive, "legacy.txt");

        Assert.Equal(0, exitCode);
        Assert.Equal(latin1, bytes);
    }

    [Fact]
    public void Cat_OutputRoundTripsThroughAdd()
    {
        var text = Encoding.UTF8.GetBytes("# Café — Über ✅\n");
        var archive = MakeArchive(("index.md", text));
        var (_, bytes) = Cat(archive, "index.md");
        var edited = Path.Combine(_tempDir, "edited.md");
        File.WriteAllBytes(edited, bytes);

        MdzArchive.AddFile(archive, "index.md", edited);

        Assert.Equal(text, MdzArchive.ReadBytes(archive, "index.md"));
    }
}
