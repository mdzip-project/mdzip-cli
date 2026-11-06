using System.CommandLine;
using MDZip.Commands;
using MDZip.Core;

namespace MDZip.Tests;

public class AddRemoveCommandTests : IDisposable
{
    private readonly string _tempDir;

    public AddRemoveCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-add-remove-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string MakeSourceDir(params (string relativePath, string content)[] files)
    {
        var dir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (relativePath, content) in files)
        {
            var filePath = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
        }
        return dir;
    }

    private string NewArchivePath() => Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".mdz");

    [Fact]
    public async Task AddCommand_AddsNewEntry_ReturnsZero()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var localFile = Path.Combine(_tempDir, "new.txt");
        await File.WriteAllTextAsync(localFile, "new content");

        var root = new RootCommand { AddCommand.Build() };
        var exitCode = await root.InvokeAsync(["add", archivePath, "assets/new.txt", localFile]);

        Assert.Equal(0, exitCode);
        var paths = MdzArchive.List(archivePath);
        Assert.Contains("assets/new.txt", paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddCommand_SecondRootMarkdown_ManifestWithoutEntryPoint_KeepsEntryPoint()
    {
        // A manifest without entryPoint, and index.md as the only root Markdown file, so the
        // entry point is index.md by discovery. With MDZip.Core 2.0.0, adding a second root .md
        // left two candidates and no recorded entry point; 2.0.1 records index.md in the manifest.
        var archivePath = NewArchivePath();
        using (var zip = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(zip.CreateEntry("index.md").Open()))
                w.Write("# Hello\n");
            using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
                w.Write("{\"spec\":{\"version\":\"1.2.0\"},\"title\":\"Test\",\"mode\":\"document\"}");
        }

        var localFile = Path.Combine(_tempDir, "second.md");
        await File.WriteAllTextAsync(localFile, "# Second\n");

        var root = new RootCommand { AddCommand.Build() };
        var exitCode = await root.InvokeAsync(["add", archivePath, "second.md", localFile]);

        Assert.Equal(0, exitCode);
        Assert.Contains("second.md", MdzArchive.List(archivePath));
        Assert.Equal("index.md", MdzArchive.ReadManifest(archivePath)?.EntryPoint);
        Assert.Equal("index.md", MdzArchive.ResolveEntryPoint(archivePath));
    }

    [Fact]
    public async Task AddCommand_MissingSourceFile_ReturnsOne()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var missingLocalFile = Path.Combine(_tempDir, "missing.txt");
        var root = new RootCommand { AddCommand.Build() };
        var exitCode = await root.InvokeAsync(["add", archivePath, "assets/missing.txt", missingLocalFile]);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RemoveCommand_RemovesExistingEntry_ReturnsZero()
    {
        var src = MakeSourceDir(
            ("index.md", "# Hello"),
            ("assets/remove-me.txt", "bye"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var root = new RootCommand { RemoveCommand.Build() };
        var exitCode = await root.InvokeAsync(["remove", archivePath, "assets/remove-me.txt"]);

        Assert.Equal(0, exitCode);
        var paths = MdzArchive.List(archivePath);
        Assert.DoesNotContain("assets/remove-me.txt", paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveCommand_MissingEntry_ReturnsOne()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archivePath = NewArchivePath();
        MdzArchive.Create(archivePath, src);

        var root = new RootCommand { RemoveCommand.Build() };
        var exitCode = await root.InvokeAsync(["remove", archivePath, "assets/not-there.txt"]);

        Assert.Equal(1, exitCode);
    }
}
