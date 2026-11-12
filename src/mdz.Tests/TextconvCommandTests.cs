using System.CommandLine;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MDZip.Commands;
using MDZip.Core;

namespace MDZip.Tests;

/// <summary>
/// <c>mdz textconv</c> prints an archive as text for Git diffs (mdzip-cli#13), in the same format as
/// mdzip.org's <c>scripts/mdz-textconv.mjs</c>.
/// </summary>
[Collection("Console")]
public sealed class TextconvCommandTests : IDisposable
{
    private readonly string _tempDir;

    public TextconvCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-textconv-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static (int ExitCode, string Text) Textconv(string archive)
    {
        var original = TextconvCommand.OpenStandardOutput;
        var output = new MemoryStream();
        TextconvCommand.OpenStandardOutput = () => output;
        try
        {
            var exitCode = new RootCommand { TextconvCommand.Build() }
                .InvokeAsync(["textconv", archive]).GetAwaiter().GetResult();
            return (exitCode, Encoding.UTF8.GetString(output.ToArray()));
        }
        finally
        {
            TextconvCommand.OpenStandardOutput = original;
        }
    }

    /// <summary>A plain ZIP with exactly these entries, in this order (no manifest added).</summary>
    private string MakeZip(params (string Path, byte[] Bytes)[] files)
    {
        var archive = Path.Combine(_tempDir, Guid.NewGuid().ToString("N")[..8] + ".mdz");
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        foreach (var (path, bytes) in files)
        {
            using var stream = zip.CreateEntry(path).Open();
            stream.Write(bytes);
        }
        return archive;
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Textconv_PrintsManifestFirstThenSortedSections()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        var archive = MakeZip(
            ("index.md", Utf8("# Title\n\nBody.\n")),
            ("images/cover.png", png),
            ("manifest.json", Utf8("{\"title\":\"T\"}")));

        var (exitCode, text) = Textconv(archive);

        var hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        Assert.Equal(0, exitCode);
        Assert.Equal(
            "===== manifest.json =====\n{\"title\":\"T\"}\n\n" +
            "===== images/cover.png =====\n[binary, 7 bytes, sha256 " + hash + "]\n\n" +
            "===== index.md =====\n# Title\n\nBody.\n",
            text);
    }

    [Fact]
    public void Textconv_SameContentInAnyEntryOrder_GivesSameText()
    {
        var a = MakeZip(("index.md", Utf8("# A\n")), ("b.md", Utf8("B\n")), ("c/d.svg", Utf8("<svg/>")));
        var b = MakeZip(("c/d.svg", Utf8("<svg/>")), ("b.md", Utf8("B\n")), ("index.md", Utf8("# A\n")));

        Assert.Equal(Textconv(a).Text, Textconv(b).Text);
    }

    [Fact]
    public void Textconv_NormalizesLineEndingsAndStripsBom()
    {
        var bomCrlf = Encoding.UTF8.GetPreamble().Concat(Utf8("one\r\ntwo\rthree")).ToArray();
        var archive = MakeZip(("index.md", bomCrlf));

        var (_, text) = Textconv(archive);

        Assert.Equal("===== index.md =====\none\ntwo\nthree\n", text);
    }

    [Fact]
    public void Textconv_ChangedImage_ChangesOnlyItsLine()
    {
        var before = Textconv(MakeZip(("index.md", Utf8("# Doc\n")), ("images/x.png", [1, 2, 3]))).Text;
        var after = Textconv(MakeZip(("index.md", Utf8("# Doc\n")), ("images/x.png", [1, 2, 4]))).Text;

        var changed = before.Split('\n').Zip(after.Split('\n')).Where(pair => pair.First != pair.Second).ToList();
        Assert.Single(changed);
        Assert.StartsWith("[binary, 3 bytes, sha256 ", changed[0].First);
    }

    [Fact]
    public void Textconv_ArchiveFromCreate_IncludesManifestAndDocument()
    {
        var src = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "index.md"), "# Hello\n");
        var archive = Path.Combine(_tempDir, "made.mdz");
        MdzArchive.Create(archive, src);

        var (exitCode, text) = Textconv(archive);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("===== manifest.json =====\n{", text);
        Assert.Contains("===== index.md =====\n# Hello\n", text);
    }

    [Fact]
    public void Textconv_NotAnArchive_PrintsItAsIsAndSucceeds()
    {
        // What Git hands the driver for a file stored in Git LFS.
        var pointer = Path.Combine(_tempDir, "pointer.mdz");
        File.WriteAllText(pointer, "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 12\n");

        var (exitCode, text) = Textconv(pointer);

        Assert.Equal(0, exitCode);
        Assert.StartsWith("[not a readable .mdz: ", text);
        Assert.EndsWith("version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 12\n", text);
    }

    [Fact]
    public void Textconv_MissingFile_Fails()
    {
        var (exitCode, text) = Textconv(Path.Combine(_tempDir, "nope.mdz"));

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, text);
    }
}
