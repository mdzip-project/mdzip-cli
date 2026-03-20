using System.CommandLine;
using Mdz.Commands;
using Mdz.Core;

namespace Mdz.Tests;

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
