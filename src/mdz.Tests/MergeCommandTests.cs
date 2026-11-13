using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using MDZip.Commands;
using MDZip.Core;

namespace MDZip.Tests;

/// <summary>
/// <c>mdz merge</c>, the Git merge driver for <c>.mdz</c> files (mdzip-cli#14). Text entries are
/// merged by <c>git merge-file</c>, so these tests need git on the PATH, as the driver does.
/// </summary>
public sealed class MergeCommandTests : IDisposable
{
    private readonly string _tempDir;

    private const string Manifest = """
        {
          "spec": { "name": "mdzip", "version": "1.2.0" },
          "title": "Notes",
          "entryPoint": "index.md"
        }
        """;

    private const string Doc = "# Notes\n\nFirst paragraph.\n\nSecond paragraph.\n\nThird paragraph.\n";

    public MergeCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-merge-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string Archive(params (string Path, string Text)[] files) =>
        ArchiveBytes(files.Select(f => (f.Path, Encoding.UTF8.GetBytes(f.Text))).ToArray());

    private string ArchiveBytes(params (string Path, byte[] Bytes)[] files)
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

    private static string Text(string archive, string path) => MdzArchive.ReadText(archive, path).Replace("\r\n", "\n");

    private static MergeCommand.MergeResult Merge(string b, string o, string t) =>
        MergeCommand.Merge(b, o, t, new MergeCommand.MergeOptions(OursLabel: "HEAD", TheirsLabel: "feature"));

    private static void AssertValid(string archive)
    {
        var result = MdzArchive.Validate(archive);
        Assert.True(result.IsValid, string.Join("\n", result.Errors));
    }

    [Fact]
    public void EditsToDifferentParagraphsMergeCleanly()
    {
        var b = Archive(("manifest.json", Manifest), ("index.md", Doc));
        var o = Archive(("manifest.json", Manifest), ("index.md", Doc.Replace("First paragraph.", "First paragraph, edited here.")));
        var t = Archive(("manifest.json", Manifest), ("index.md", Doc.Replace("Third paragraph.", "Third paragraph, edited there.")));

        var result = Merge(b, o, t);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Conflicts);
        var merged = Text(o, "index.md");
        Assert.Contains("First paragraph, edited here.", merged);
        Assert.Contains("Third paragraph, edited there.", merged);
        AssertValid(o);
    }

    [Fact]
    public void OverlappingEditsGetMarkersWithTheBase()
    {
        var b = Archive(("manifest.json", Manifest), ("index.md", Doc));
        var o = Archive(("manifest.json", Manifest), ("index.md", Doc.Replace("Second paragraph.", "Second paragraph, our way.")));
        var t = Archive(("manifest.json", Manifest), ("index.md", Doc.Replace("Second paragraph.", "Second paragraph, their way.")));

        var result = Merge(b, o, t);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Conflicts, c => c.StartsWith("index.md: 1 conflict"));
        var merged = Text(o, "index.md");
        Assert.Contains("<<<<<<< HEAD:index.md\nSecond paragraph, our way.\n", merged);
        Assert.Contains("||||||| base:index.md\nSecond paragraph.\n", merged);
        Assert.Contains("=======\nSecond paragraph, their way.\n>>>>>>> feature:index.md\n", merged);
        // The archive itself stays valid; only the document holds markers.
        AssertValid(o);
    }

    [Fact]
    public void AddedAndRemovedEntriesAreApplied()
    {
        var b = Archive(("manifest.json", Manifest), ("index.md", Doc), ("old.md", "old\n"), ("keep.md", "keep\n"));
        var o = Archive(("manifest.json", Manifest), ("index.md", Doc), ("keep.md", "keep\n"), ("ours.md", "ours\n"));
        var t = Archive(("manifest.json", Manifest), ("index.md", Doc), ("old.md", "old\n"), ("keep.md", "keep\n"), ("theirs.md", "theirs\n"));

        var result = Merge(b, o, t);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            ["index.md", "keep.md", "manifest.json", "ours.md", "theirs.md"],
            MdzArchive.List(o).Order(StringComparer.Ordinal));
        // manifest.json is written first.
        using var zip = ZipFile.OpenRead(o);
        Assert.Equal("manifest.json", zip.Entries[0].FullName);
    }

    [Fact]
    public void AnImageChangedOnOneSideTakesThatSide()
    {
        var b = ArchiveBytes(("index.md", Encoding.UTF8.GetBytes(Doc)), ("images/chart.png", [1, 2, 3]));
        var o = ArchiveBytes(("index.md", Encoding.UTF8.GetBytes(Doc)), ("images/chart.png", [1, 2, 3]));
        var t = ArchiveBytes(("index.md", Encoding.UTF8.GetBytes(Doc)), ("images/chart.png", [9, 9, 9, 9]));

        Assert.Equal(0, Merge(b, o, t).ExitCode);
        Assert.Equal([9, 9, 9, 9], MdzArchive.ReadBytes(o, "images/chart.png"));
    }

    [Fact]
    public void AnImageChangedOnBothSidesKeepsOursAndTheirsBesideIt()
    {
        var b = ArchiveBytes(("index.md", Encoding.UTF8.GetBytes(Doc)), ("images/chart.png", [1, 2, 3]));
        var o = ArchiveBytes(("index.md", Encoding.UTF8.GetBytes(Doc)), ("images/chart.png", [4, 4]));
        var t = ArchiveBytes(("index.md", Encoding.UTF8.GetBytes(Doc)), ("images/chart.png", [5, 5, 5]));

        var result = Merge(b, o, t);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal([4, 4], MdzArchive.ReadBytes(o, "images/chart.png"));
        Assert.Equal([5, 5, 5], MdzArchive.ReadBytes(o, "images/chart.theirs.png"));
        Assert.Contains(result.Conflicts, c => c.Contains("images/chart.theirs.png"));
    }

    [Fact]
    public void AnEntryDeletedOnOneSideAndChangedOnTheOtherIsKept()
    {
        var b = Archive(("index.md", Doc), ("notes.md", "notes\n"));
        var o = Archive(("index.md", Doc));
        var t = Archive(("index.md", Doc), ("notes.md", "notes, expanded\n"));

        var result = Merge(b, o, t);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("notes, expanded\n", Text(o, "notes.md"));
        Assert.Contains(result.Conflicts, c => c.StartsWith("notes.md: deleted in HEAD and changed in feature"));
    }

    [Fact]
    public void ManifestFieldsMergeAndTimesTakeTheLaterModifiedAndEarlierCreated()
    {
        var b = Archive(("manifest.json", """{ "title": "Notes", "entryPoint": "index.md", "created": "2026-01-01T00:00:00Z", "modified": "2026-01-01T00:00:00Z" }"""), ("index.md", Doc));
        var o = Archive(("manifest.json", """{ "title": "Team notes", "entryPoint": "index.md", "created": "2026-02-01T00:00:00Z", "modified": "2026-03-01T00:00:00Z" }"""), ("index.md", Doc));
        var t = Archive(("manifest.json", """{ "title": "Notes", "entryPoint": "index.md", "language": "en", "created": "2026-01-15T00:00:00Z", "modified": "2026-04-01T00:00:00Z" }"""), ("index.md", Doc));

        var result = Merge(b, o, t);

        Assert.Equal(0, result.ExitCode);
        var manifest = JsonNode.Parse(Text(o, "manifest.json"))!;
        Assert.Equal("Team notes", (string?)manifest["title"]);
        Assert.Equal("en", (string?)manifest["language"]);
        Assert.Equal("2026-04-01T00:00:00Z", (string?)manifest["modified"]);
        // Re-packing rewrites "created" on both sides: the earlier time wins.
        Assert.Equal("2026-01-15T00:00:00Z", (string?)manifest["created"]);
    }

    [Fact]
    public void AManifestFieldSetDifferentlyOnBothSidesKeepsOurs()
    {
        var b = Archive(("manifest.json", """{ "title": "Notes", "entryPoint": "index.md" }"""), ("index.md", Doc));
        var o = Archive(("manifest.json", """{ "title": "Ours", "entryPoint": "index.md" }"""), ("index.md", Doc));
        var t = Archive(("manifest.json", """{ "title": "Theirs", "entryPoint": "index.md" }"""), ("index.md", Doc));

        var result = Merge(b, o, t);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("Ours", (string?)JsonNode.Parse(Text(o, "manifest.json"))!["title"]);
        Assert.Contains(result.Conflicts, c => c.Contains("\"title\"") && c.Contains("\"Theirs\""));
        AssertValid(o);
    }

    [Fact]
    public void ManifestFilesMergeByPath()
    {
        var b = Archive(("manifest.json", """{ "entryPoint": "index.md", "files": [ { "path": "index.md", "title": "Home" } ] }"""), ("index.md", Doc));
        var o = Archive(("manifest.json", """{ "entryPoint": "index.md", "files": [ { "path": "index.md", "title": "Start" }, { "path": "a.md" } ] }"""), ("index.md", Doc));
        var t = Archive(("manifest.json", """{ "entryPoint": "index.md", "files": [ { "path": "index.md", "title": "Home" }, { "path": "b.md" } ] }"""), ("index.md", Doc));

        Assert.Equal(0, Merge(b, o, t).ExitCode);
        var files = JsonNode.Parse(Text(o, "manifest.json"))!["files"]!.AsArray();
        Assert.Equal(["index.md", "a.md", "b.md"], files.Select(f => (string)f!["path"]!));
        Assert.Equal("Start", (string?)files[0]!["title"]);
    }

    [Fact]
    public void BothSidesAddingTheArchiveMergesWithAnEmptyBase()
    {
        var b = Path.Combine(_tempDir, "empty-base");
        File.WriteAllBytes(b, []);
        var o = Archive(("index.md", "# Ours\n"));
        var t = Archive(("index.md", "# Theirs\n"), ("extra.md", "extra\n"));

        var result = Merge(b, o, t);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("<<<<<<< HEAD:index.md", Text(o, "index.md"));
        Assert.Equal("extra\n", Text(o, "extra.md"));
    }

    [Fact]
    public void AnUnreadableSideLeavesOursUntouched()
    {
        var b = Archive(("index.md", Doc));
        var o = Archive(("index.md", Doc + "ours\n"));
        var t = Path.Combine(_tempDir, "lfs-pointer");
        File.WriteAllText(t, "version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 12\n");
        var before = File.ReadAllBytes(o);

        var result = Merge(b, o, t);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(before, File.ReadAllBytes(o));
        Assert.Contains(result.Messages, m => m.Contains("git checkout --ours"));
    }
}
