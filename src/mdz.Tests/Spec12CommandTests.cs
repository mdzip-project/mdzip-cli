using System.CommandLine;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MDZip.Commands;
using MDZip.Core;

namespace MDZip.Tests;

/// <summary>
/// Spec 1.2.0 behaviour of the mdz commands (mdzip-cli#7): producers always write a complete
/// manifest (§14.1), paths are case-sensitive (§5.4.1), entry point errors never fall back (§5.5),
/// and the shared fixtures for entry sets (T-013), round trips (P-004, P-005) and the generated
/// archives (T-014, T-015, T-017, P-008).
/// </summary>
public sealed class Spec12CommandTests : IDisposable
{
    private static readonly object ConsoleLock = new();
    private readonly string _tempDir;

    public Spec12CommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-spec12-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static RootCommand Root() => new()
    {
        CreateCommand.Build(),
        AddCommand.Build(),
        RemoveCommand.Build(),
        ValidateCommand.Build(),
        CatCommand.Build(),
        ExtractCommand.Build(),
        ManifestCommand.Build(),
    };

    /// <summary>Runs mdz with the given arguments and returns its exit code, stdout and stderr.</summary>
    private static (int ExitCode, string Out, string Error) Run(params string[] args)
    {
        lock (ConsoleLock)
        {
            var (oldOut, oldError) = (Console.Out, Console.Error);
            var (output, error) = (new StringWriter(), new StringWriter());
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                var exitCode = Root().InvokeAsync(args).GetAwaiter().GetResult();
                return (exitCode, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(oldOut);
                Console.SetError(oldError);
            }
        }
    }

    private string MakeSourceDir(params (string RelativePath, string Content)[] files)
    {
        var dir = Path.Combine(_tempDir, "src-" + Guid.NewGuid().ToString("N")[..8]);
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

    private string TempFile(string text) => TempFile(Encoding.UTF8.GetBytes(text));

    private string TempFile(byte[] bytes)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string FixtureCopy(string name)
    {
        var path = NewArchivePath();
        File.Copy(Fixture(name), path);
        return path;
    }

    private static string Fixture(string name) => Path.Combine(SpecFixtureTests.FixturesDirectory, name);

    private static JsonNode LoadNode(string name) => JsonNode.Parse(File.ReadAllText(Fixture(name)))!;

    private static JsonObject ReadManifestJson(string archive) =>
        JsonNode.Parse(MdzArchive.ReadText(archive, "manifest.json"))!.AsObject();

    // -------------------------------------------------------------------------
    // create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_WithoutOptions_WritesACompleteManifest()
    {
        var src = MakeSourceDir(("guide.md", "# Guide"), ("README.md", "# Readme"));
        var archive = NewArchivePath();

        var (exitCode, _, error) = Run("create", src, archive, "--no-interactive");

        Assert.True(exitCode == 0, error);
        var manifest = ReadManifestJson(archive);
        Assert.Equal(MdzSpec.SpecVersion, manifest["spec"]!["version"]!.GetValue<string>());
        Assert.Equal(Path.GetFileName(src), manifest["title"]!.GetValue<string>());
        Assert.Equal("document", manifest["mode"]!.GetValue<string>());
        // README.md is a support file, set aside in entry point discovery (§5.5).
        Assert.Equal("guide.md", manifest["entryPoint"]!.GetValue<string>());
    }

    [Fact]
    public void Create_WithASourceManifest_KeepsItAndCompletesIt()
    {
        var src = MakeSourceDir(
            ("start.md", "# Start"),
            ("other.md", "# Other"),
            ("manifest.json", """{"title": "Mine", "entryPoint": "start.md", "x-tool": {"keep": [1, 2]}}"""));
        var archive = NewArchivePath();

        // The default filters package Markdown and images only, so the manifest is named explicitly.
        var (exitCode, _, error) = Run("create", src, archive, "--filter", "**/*.md", "--filter", "manifest.json", "--no-interactive");

        Assert.True(exitCode == 0, error);
        var manifest = ReadManifestJson(archive);
        Assert.Equal("Mine", manifest["title"]!.GetValue<string>());
        Assert.Equal("start.md", manifest["entryPoint"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"keep": [1, 2]}"""), manifest["x-tool"]));
        Assert.Equal(MdzSpec.SpecVersion, manifest["spec"]!["version"]!.GetValue<string>());
    }

    [Fact]
    public void Create_WithMetadataOptions_WritesTheCurrentSpecVersion()
    {
        var src = MakeSourceDir(("index.md", "# Hello"));
        var archive = NewArchivePath();

        var (exitCode, _, error) = Run("create", src, archive, "--title", "Hello", "--no-interactive");

        Assert.True(exitCode == 0, error);
        var manifest = ReadManifestJson(archive);
        Assert.Equal(MdzSpec.SpecVersion, manifest["spec"]!["version"]!.GetValue<string>());
        Assert.Equal("index.md", manifest["entryPoint"]!.GetValue<string>());
    }

    [Fact]
    public void Create_EntryPointDifferingOnlyInCase_IsMissingWithAHint()
    {
        var src = MakeSourceDir(("index.md", "# Hello"), ("other.md", "# Other"));
        var archive = NewArchivePath();

        var (exitCode, _, error) = Run("create", src, archive, "--entry-point", "Index.md", "--no-interactive");

        Assert.Equal(1, exitCode);
        Assert.Contains("ERR_ENTRYPOINT_MISSING", error);
        Assert.Contains("Did you mean 'index.md'?", error);
        Assert.False(File.Exists(archive));
    }

    [Fact]
    public void Create_EntryPointThatIsNotMarkdown_IsInvalid()
    {
        var src = MakeSourceDir(("index.md", "# Hello"), ("cover.png", "png"));
        var archive = NewArchivePath();

        var (exitCode, _, error) = Run("create", src, archive, "--entry-point", "cover.png", "--no-interactive");

        Assert.Equal(1, exitCode);
        Assert.Contains("ERR_ENTRYPOINT_INVALID", error);
    }

    // -------------------------------------------------------------------------
    // validate / cat / extract
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NoManifest_IsANoteNotAWarning()
    {
        var archive = NewArchivePath();
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var s = zip.CreateEntry("index.md").Open();
            s.Write(Encoding.UTF8.GetBytes("# Hello"));
        }

        var (exitCode, output, _) = Run("validate", archive);

        Assert.Equal(0, exitCode);
        Assert.Contains("Status: valid", output);
        Assert.Contains("a conforming producer would have written one", output);
        Assert.DoesNotContain("Warning:", output);
    }

    [Fact]
    public void Validate_ManifestEntryPointDifferingOnlyInCase_IsMissingWithAHint()
    {
        var archive = NewArchivePath();
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var s = zip.CreateEntry("index.md").Open())
                s.Write(Encoding.UTF8.GetBytes("# Hello"));
            using var m = zip.CreateEntry("manifest.json").Open();
            m.Write(Encoding.UTF8.GetBytes("""{"entryPoint": "Index.md"}"""));
        }

        var (exitCode, _, error) = Run("validate", archive);

        Assert.Equal(1, exitCode);
        Assert.Contains("ERR_ENTRYPOINT_MISSING", error);
        Assert.Contains("did you mean \"index.md\"?", error);
    }

    [Fact]
    public void Cat_EntryDifferingOnlyInCase_FailsWithAHint()
    {
        var src = MakeSourceDir(("index.md", "# Hello"), ("docs/Guide.md", "# Guide"));
        var archive = NewArchivePath();
        MdzArchive.Create(archive, src);

        var (exitCode, _, error) = Run("cat", archive, "docs/guide.md");

        Assert.Equal(1, exitCode);
        Assert.Contains("Did you mean 'docs/Guide.md'?", error);
        Assert.Equal(0, Run("cat", archive, "docs/Guide.md").ExitCode);
    }

    [Fact]
    public void T013_EntrySetCases_ValidateReportsDuplicatesAndConflicts()
    {
        foreach (var c in LoadNode("entry-set-cases.json")["cases"]!.AsArray())
        {
            var name = c!["name"]!.GetValue<string>();
            var entries = c["entries"]!.AsArray().Select(e => e!.GetValue<string>()).ToList();
            var archive = NewArchivePath();
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (var entry in entries)
                {
                    var created = zip.CreateEntry(entry);
                    if (!entry.EndsWith('/'))
                    {
                        using var writer = new StreamWriter(created.Open());
                        writer.Write("# x\n");
                    }
                }
                using var index = new StreamWriter(zip.CreateEntry(entries.Contains("index.md") ? "other.md" : "index.md").Open());
                index.Write("# i\n");
            }

            var (exitCode, _, error) = Run("validate", archive);

            if (c["valid"]!.GetValue<bool>())
            {
                Assert.True(exitCode == 0, $"{name}: {error}");
            }
            else
            {
                Assert.True(exitCode == 1, name);
                Assert.Contains(c["errorCode"]!.GetValue<string>(), error);
            }
        }
    }

    [Fact]
    public void T014_UnsupportedCompression_IsZipInvalid()
    {
        var (exitCode, _, error) = Run("validate", Fixture("unsupported-compression.mdz"));

        Assert.Equal(1, exitCode);
        Assert.Contains("ERR_ZIP_INVALID", error);
        Assert.Equal(1, Run("cat", Fixture("unsupported-compression.mdz"), "index.md").ExitCode);
    }

    [Fact]
    public void T015_ProjectMode_IsValid()
    {
        var (exitCode, output, error) = Run("validate", Fixture("project-mode.mdz"));

        Assert.True(exitCode == 0, error);
        Assert.Contains("is valid", output);
    }

    [Fact]
    public void T017_SymbolicLinks_AreReportedSkippedAndNeverExtractedAsLinks()
    {
        var c = LoadNode("archive-cases.json")["cases"]!.AsArray().First(x => x!["id"]!.GetValue<string>() == "symlink-entries")!;
        var archive = Fixture(c["archive"]!.GetValue<string>());
        var symlinks = c["symlinks"]!.AsObject().Select(p => p.Key).ToList();

        var (exitCode, output, error) = Run("validate", archive);
        Assert.True(exitCode == 0, error);
        foreach (var name in symlinks)
            Assert.Contains($"'{name}'", output);

        Assert.Equal(1, Run("cat", archive, "shortcut.md").ExitCode);

        var root = Path.Combine(_tempDir, "extract-root");
        var destination = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(root);
        var extract = Run("extract", archive, "-o", destination);
        Assert.True(extract.ExitCode == 0, extract.Error);
        Assert.True(File.Exists(Path.Combine(destination, "index.md")));

        // Nothing outside the destination, and no link created: each name is absent or a regular
        // file holding the stored target text.
        Assert.Equal(["a"], Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName));
        foreach (var (name, target) in c["symlinks"]!.AsObject())
        {
            var path = Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(path))
                Assert.Fail($"{name} was extracted as a directory");
            if (!File.Exists(path))
                continue;
            Assert.False(new FileInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint), name);
            Assert.Equal(target!.GetValue<string>(), File.ReadAllText(path));
        }
    }

    [Fact]
    public void P008_AddKeepsSymbolicLinkEntries()
    {
        var archive = FixtureCopy("symlink-entries.mdz");

        var (exitCode, _, error) = Run("add", archive, "notes.md", TempFile("# Notes\n\nEdited.\n"));

        Assert.True(exitCode == 0, error);
        Assert.Equal(3, MdzArchive.ListSpecialFiles(archive).Count);
    }

    // -------------------------------------------------------------------------
    // add / remove / manifest set
    // -------------------------------------------------------------------------

    [Fact]
    public void P004_P005_RoundTripFlows_SavedTwice()
    {
        var expectations = LoadNode("round-trip-expectations.json");
        foreach (var flow in expectations["flows"]!.AsArray())
        {
            var flowName = flow!["name"]!.GetValue<string>();
            var archive = FixtureCopy("round-trip.mdz");
            for (var round = 1; round <= 2; round++)
            {
                var label = $"{flowName} (save {round})";
                var (exitCode, _, error) = RunFlow(flowName, archive, round);
                Assert.True(exitCode == 0, $"{label}: {error}");
                CheckRoundTrip(expectations, flow, archive, label);
            }
        }
    }

    private (int ExitCode, string Out, string Error) RunFlow(string flow, string archive, int round)
    {
        switch (flow)
        {
            case "open and save without an edit":
                // mdz has no plain "save"; rewriting a manifest field with its own value is the closest.
                return Run("manifest", "set", archive, "--title", MdzArchive.ReadManifest(archive)!.Title!);
            case "change a recognized manifest field":
                return Run("manifest", "set", archive, "--title", "Edited title");
            case "edit document content":
                return Run("add", archive, "index.md", TempFile(MdzArchive.ReadText(archive, "index.md") + "\nEdited.\n"));
            case "add a resource":
                return Run("add", archive, $"images/added-{round}.png", TempFile([0x89, 0x50, 0x4e, 0x47]));
            case "remove a recognized resource":
                if (round == 2)
                    return Run("manifest", "set", archive, "--title", "Second save");
                // Remove the link, then the file (the flow lists index.md in changes).
                var index = MdzArchive.ReadText(archive, "index.md").Replace("See [the guide](docs/guide.md).", string.Empty);
                var edit = Run("add", archive, "index.md", TempFile(index));
                return edit.ExitCode != 0 ? edit : Run("remove", archive, "docs/guide.md");
            default:
                throw new InvalidOperationException($"unknown flow {flow}");
        }
    }

    private static void CheckRoundTrip(JsonNode expectations, JsonNode flow, string archive, string label)
    {
        var manifest = ReadManifestJson(archive);
        foreach (var item in expectations["preservedManifestValues"]!.AsArray())
        {
            JsonNode? node = manifest;
            foreach (var key in item!["pointer"]!.GetValue<string>().Split('/').Skip(1))
                node = node?[key.Replace("~1", "/").Replace("~0", "~")];
            Assert.True(JsonNode.DeepEquals(node, item["value"]), $"{label}: {item["pointer"]}");
        }

        var changes = flow["changes"]?.AsArray().Select(c => c!.GetValue<string>()).ToHashSet() ?? [];
        var removes = flow["removes"]?.AsArray().Select(c => c!.GetValue<string>()).ToHashSet() ?? [];
        foreach (var removed in removes)
            Assert.False(MdzArchive.HasEntry(archive, removed), $"{label}: {removed} should be removed");

        foreach (var entry in expectations["preservedEntries"]!.AsArray())
        {
            var path = entry!["path"]!.GetValue<string>();
            if (changes.Contains(path) || removes.Contains(path))
                continue;
            var bytes = MdzArchive.ReadBytes(archive, path);
            Assert.True(bytes.Length == entry["bytes"]!.GetValue<int>(), $"{label}: {path} length");
            Assert.True(
                Convert.ToHexString(SHA256.HashData(bytes)).Equals(entry["sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase),
                $"{label}: {path} sha256");
        }
    }

    [Fact]
    public void Remove_ThatWouldMoveTheEntryPoint_IsRefused()
    {
        var archive = NewArchivePath();
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            foreach (var name in new[] { "index.md", "a.md", "b.md" })
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(Encoding.UTF8.GetBytes($"# {name}"));
            }
        }

        var (exitCode, _, error) = Run("remove", archive, "index.md");

        Assert.Equal(1, exitCode);
        Assert.Contains("ERR_ENTRYPOINT", error);
        Assert.True(MdzArchive.HasEntry(archive, "index.md"));
    }

    [Fact]
    public void Add_ToAnArchiveWithoutAManifest_AddsOneKeepingTheEntryPoint()
    {
        var archive = NewArchivePath();
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var s = zip.CreateEntry("notes.md").Open();
            s.Write(Encoding.UTF8.GetBytes("# Notes"));
        }

        var (exitCode, _, error) = Run("add", archive, "AGENTS.md", TempFile("# Agents\n"));

        Assert.True(exitCode == 0, error);
        var manifest = ReadManifestJson(archive);
        Assert.Equal("notes.md", manifest["entryPoint"]!.GetValue<string>());
        Assert.Equal(MdzSpec.SpecVersion, manifest["spec"]!["version"]!.GetValue<string>());
    }
}
