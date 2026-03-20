using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Mdz.Core;
using Mdz.Models;

namespace Mdz.Tests;

public class MdzArchiveTests : IDisposable
{
    private readonly string _tempDir;
    private static string SampleArchivePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "demo.mdz"));

    public MdzArchiveTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private string MakeSourceDir(params (string relativePath, string content)[] files)
    {
        var dir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (relativePath, content) in files)
        {
            var filePath = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        return dir;
    }

    private string NewArchivePath() => Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".mdz");

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_MinimalArchive_ProducesValidZip()
    {
        var src = MakeSourceDir(("index.md", "# Hello World\n\nThis is a test."));
        var archivePath = NewArchivePath();

        MdzArchive.Create(archivePath, src);

        Assert.True(File.Exists(archivePath));
        using var zip = ZipFile.OpenRead(archivePath);
        Assert.Single(zip.Entries, e => e.Name == "index.md");
    }

    [Fact]
    public void Create_WithManifest_WritesManifestJson()
    {
        var src = MakeSourceDir(("index.md", "# Test"));
        var archivePath = NewArchivePath();
        var manifest = new Manifest { Spec = new ManifestSpec { Version = "1.0.0" }, Title = "Test Doc", EntryPoint = "index.md" };

        MdzArchive.Create(archivePath, src, manifest);

        using var zip = ZipFile.OpenRead(archivePath);
        var mEntry = zip.Entries.FirstOrDefault(e => e.Name == "manifest.json");
        Assert.NotNull(mEntry);
    }

    [Fact]
    public void Create_NormalisesLineEndingsToLf()
    {
        var src = MakeSourceDir(("index.md", "# Hello\r\nWorld\r\n"));
        var archivePath = NewArchivePath();

        MdzArchive.Create(archivePath, src);

        using var zip = ZipFile.OpenRead(archivePath);
        var entry = zip.Entries.First(e => e.Name == "index.md");
        using var stream = entry.Open();
        var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        Assert.DoesNotContain("\r\n", text);
        Assert.Contains("\n", text);
    }

    [Fact]
    public void Create_WithAssets_IncludesAllFiles()
    {
        var src = MakeSourceDir(
            ("index.md", "# Doc\n\n![image](assets/images/pic.png)"),
            ("assets/images/pic.png", "fake-png-data"));
        var archivePath = NewArchivePath();

        MdzArchive.Create(archivePath, src);

        var paths = MdzArchive.List(archivePath);
        Assert.Contains("index.md", paths);
        Assert.Contains("assets/images/pic.png", paths);
    }

    [Fact]
    public void Create_NoIndexAndAmbiguousRootMarkdown_Throws()
    {
        var src = MakeSourceDir(("doc1.md", "# One"), ("doc2.md", "# Two"));
        var archivePath = NewArchivePath();

        var ex = Assert.Throws<InvalidOperationException>(() => MdzArchive.Create(archivePath, src));
        Assert.Contains("No unambiguous entry point", ex.Message);
    }

    [Fact]
    public void Create_ManifestEntryPointMissing_Throws()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        var manifest = new Manifest { Spec = new ManifestSpec { Version = "1.0.0" }, Title = "Doc", EntryPoint = "missing.md" };

        var ex = Assert.Throws<InvalidOperationException>(() => MdzArchive.Create(archivePath, src, manifest));
        Assert.Contains("entryPoint", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Extract
    // -------------------------------------------------------------------------

    [Fact]
    public void Extract_WritesFilesToDestination()
    {
        var src = MakeSourceDir(("index.md", "# Hello"), ("assets/style.css", "body{}"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var destDir = Path.Combine(_tempDir, "extracted");
        MdzArchive.Extract(archivePath, destDir);

        Assert.True(File.Exists(Path.Combine(destDir, "index.md")));
        Assert.True(File.Exists(Path.Combine(destDir, "assets", "style.css")));
    }

    [Fact]
    public void Extract_PathTraversalEntry_Throws()
    {
        // Manually create a zip with a traversal path
        var archivePath = NewArchivePath();
        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("../evil.txt");
            using var s = entry.Open();
            s.Write(Encoding.UTF8.GetBytes("evil"));
        }

        var destDir = Path.Combine(_tempDir, "ext-traversal");
        Assert.Throws<InvalidOperationException>(() => MdzArchive.Extract(archivePath, destDir));
    }

    // -------------------------------------------------------------------------
    // List
    // -------------------------------------------------------------------------

    [Fact]
    public void List_ReturnsAllFilePaths()
    {
        var src = MakeSourceDir(("index.md", "# Hello"), ("chapter-01.md", "# Chapter 1"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var paths = MdzArchive.List(archivePath);

        Assert.Contains("index.md", paths);
        Assert.Contains("chapter-01.md", paths);
    }

    [Fact]
    public void ListDetailed_ReturnsEntryMetadata()
    {
        var src = MakeSourceDir(("index.md", "# Hello World"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var entries = MdzArchive.ListDetailed(archivePath);

        Assert.Single(entries);
        Assert.Equal("index.md", entries[0].Path);
        Assert.True(entries[0].Size > 0);
    }

    // -------------------------------------------------------------------------
    // ReadManifest
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadManifest_NoManifest_ReturnsNull()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var manifest = MdzArchive.ReadManifest(archivePath);

        Assert.Null(manifest);
    }

    [Fact]
    public void ReadManifest_WithManifest_ReturnsManifest()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        var manifest = new Manifest { Spec = new ManifestSpec { Version = "1.0.0" }, Title = "My Doc" };
        MdzArchive.Create(archivePath, src, manifest);

        var read = MdzArchive.ReadManifest(archivePath);

        Assert.NotNull(read);
        Assert.Equal("1.0.0", read.Spec?.Version);
        Assert.Equal("My Doc", read.Title);
    }

    // -------------------------------------------------------------------------
    // ResolveEntryPoint
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveEntryPoint_IndexMdPresent_ReturnsIndexMd()
    {
        var src = MakeSourceDir(("index.md", "# Hello"), ("other.md", "# Other"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var entryPoint = MdzArchive.ResolveEntryPoint(archivePath);

        Assert.Equal("index.md", entryPoint);
    }

    [Fact]
    public void ResolveEntryPoint_SingleRootMarkdown_ReturnsThatFile()
    {
        var src = MakeSourceDir(("readme.md", "# Readme"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var entryPoint = MdzArchive.ResolveEntryPoint(archivePath);

        Assert.Equal("readme.md", entryPoint);
    }

    [Fact]
    public void ResolveEntryPoint_ManifestOverride_ReturnsManifestEntryPoint()
    {
        var src = MakeSourceDir(("index.md", "# Index"), ("start.md", "# Start"));
        var archivePath = NewArchivePath();
        var manifest = new Manifest { Spec = new ManifestSpec { Version = "1.0.0" }, Title = "Doc", EntryPoint = "start.md" };
        MdzArchive.Create(archivePath, src, manifest);

        var entryPoint = MdzArchive.ResolveEntryPoint(archivePath);

        Assert.Equal("start.md", entryPoint);
    }

    [Fact]
    public void ResolveEntryPoint_MultipleRootMarkdownNoIndex_ReturnsNull()
    {
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var e1 = zip.CreateEntry("doc1.md");
            using (var s = e1.Open()) s.Write(Encoding.UTF8.GetBytes("# Doc 1"));
            var e2 = zip.CreateEntry("doc2.md");
            using (var s = e2.Open()) s.Write(Encoding.UTF8.GetBytes("# Doc 2"));
        }

        var entryPoint = MdzArchive.ResolveEntryPoint(archivePath);

        Assert.Null(entryPoint);
    }

    // -------------------------------------------------------------------------
    // Validate
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_MinimalValidArchive_IsValid()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var result = MdzArchive.Validate(archivePath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithValidManifest_IsValid()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        var manifest = new Manifest { Spec = new ManifestSpec { Version = "1.0.0" }, Title = "Test", EntryPoint = "index.md" };
        MdzArchive.Create(archivePath, src, manifest);

        var result = MdzArchive.Validate(archivePath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ManifestMissingSpecVersion_IsValidWithWarning()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();

        // Write a manifest manually without the spec.version field
        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var idxEntry = zip.CreateEntry("index.md");
            using (var s = idxEntry.Open()) s.Write(Encoding.UTF8.GetBytes("# Hello"));

            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { title = "Test" })));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("spec.version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ManifestMissingTitleField_IsValid()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var idxEntry = zip.CreateEntry("index.md");
            using (var s = idxEntry.Open()) s.Write(Encoding.UTF8.GetBytes("# Hello"));

            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { spec = new { version = "1.0.0" } })));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.Contains("title", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ManifestEntryPointMissing_IsInvalid()
    {
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var idxEntry = zip.CreateEntry("index.md");
            using (var s = idxEntry.Open()) s.Write(Encoding.UTF8.GetBytes("# Hello"));

            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { spec = new { version = "1.0.0" }, title = "Doc", entryPoint = "nonexistent.md" })));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ERR_ENTRYPOINT_MISSING"));
    }

    [Fact]
    public void Validate_UnsupportedMajorVersion_IsInvalid()
    {
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var idxEntry = zip.CreateEntry("index.md");
            using (var s = idxEntry.Open()) s.Write(Encoding.UTF8.GetBytes("# Hello"));

            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { spec = new { version = "9.0.0" }, title = "Doc" })));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ERR_VERSION_UNSUPPORTED"));
    }

    [Fact]
    public void Validate_SpecVersionNotSemVer_IsInvalid()
    {
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var idxEntry = zip.CreateEntry("index.md");
            using (var s = idxEntry.Open()) s.Write(Encoding.UTF8.GetBytes("# Hello"));

            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { spec = new { version = "1" }, title = "Doc" })));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ERR_MANIFEST_INVALID") && e.Contains("not a valid semver string"));
    }

    [Fact]
    public void Validate_SpecVersionSemVerPrereleaseWithBuild_IsValid()
    {
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var idxEntry = zip.CreateEntry("index.md");
            using (var s = idxEntry.Open()) s.Write(Encoding.UTF8.GetBytes("# Hello"));

            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                spec = new { version = "1.0.0-alpha.1+exp.sha.5114f85" },
                title = "Doc"
            })));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NoEntryPoint_IsInvalid()
    {
        var archivePath = NewArchivePath();

        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            // Two root markdown files - ambiguous
            var e1 = zip.CreateEntry("doc1.md");
            using (var s = e1.Open()) s.Write(Encoding.UTF8.GetBytes("# Doc 1"));
            var e2 = zip.CreateEntry("doc2.md");
            using (var s = e2.Open()) s.Write(Encoding.UTF8.GetBytes("# Doc 2"));
        }

        var result = MdzArchive.Validate(archivePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ERR_ENTRYPOINT_UNRESOLVED"));
    }

    [Fact]
    public void Validate_NotAZipFile_IsInvalid()
    {
        var archivePath = NewArchivePath();
        File.WriteAllText(archivePath, "this is not a zip file");

        var result = MdzArchive.Validate(archivePath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ERR_ZIP_INVALID"));
    }

    // -------------------------------------------------------------------------
    // Sample Archive
    // -------------------------------------------------------------------------

    [Fact]
    public void SampleArchive_List_IncludesExpectedFiles()
    {
        Assert.True(File.Exists(SampleArchivePath), $"Sample archive not found at '{SampleArchivePath}'.");

        var paths = MdzArchive.List(SampleArchivePath);

        Assert.Contains("index.md", paths);
        Assert.Contains("manifest.json", paths);
        Assert.Contains("assets/overview.svg", paths);
    }

    [Fact]
    public void SampleArchive_Validate_IsValid()
    {
        Assert.True(File.Exists(SampleArchivePath), $"Sample archive not found at '{SampleArchivePath}'.");

        var result = MdzArchive.Validate(SampleArchivePath);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

}
