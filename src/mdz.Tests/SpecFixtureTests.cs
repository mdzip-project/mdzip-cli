using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Mdz.Commands;
using Mdz.Core;

namespace Mdz.Tests;

public sealed class SpecFixtureTests : IDisposable
{
    private readonly string _tempDir;

    public SpecFixtureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-spec-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void EntryPointCases_MatchFixtures()
    {
        var cases = LoadFixture<List<EntryPointCase>>("entrypoint-cases.json");
        Assert.NotNull(cases);

        foreach (var c in cases!)
        {
            var archivePath = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".mdz");
            CreateArchiveForEntryPointCase(archivePath, c);

            var actual = MdzArchive.ResolveEntryPoint(archivePath);
            Assert.Equal(c.ExpectedEntryPoint, actual);
        }
    }

    [Fact]
    public void PathValidationCases_MatchFixtures()
    {
        var cases = LoadFixture<List<PathValidationCase>>("path-validation-cases.json");
        Assert.NotNull(cases);

        foreach (var c in cases!)
        {
            var error = PathValidator.Validate(c.Path);
            Assert.Equal(c.Valid, error is null);

            if (!c.Valid && !string.IsNullOrWhiteSpace(c.ErrorCode))
            {
                var expectedSnippet = c.ErrorCode switch
                {
                    "ERR_PATH_LEADING_SLASH" => "leading slash",
                    "ERR_PATH_TRAVERSAL" => "path traversal",
                    "ERR_PATH_RESERVED_CHAR" => "OS-reserved character",
                    "ERR_PATH_CONTROL_CHAR" => "control characters",
                    _ => null
                };

                if (expectedSnippet is not null)
                    Assert.Contains(expectedSnippet, error!, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void CreateFilterCases_MatchFixtures()
    {
        var cases = LoadFixture<List<CreateFilterCase>>("create-filter-cases.json");
        Assert.NotNull(cases);

        var getEffectiveFilters = typeof(CreateCommand).GetMethod(
            "GetEffectiveFilters",
            BindingFlags.NonPublic | BindingFlags.Static);
        var matchesAnyFilter = typeof(CreateCommand).GetMethod(
            "MatchesAnyFilter",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(getEffectiveFilters);
        Assert.NotNull(matchesAnyFilter);

        foreach (var c in cases!)
        {
            var effective = (IReadOnlyList<string>)getEffectiveFilters!.Invoke(null, [c.Filters.ToArray()])!;

            var included = c.InputPaths
                .Where(path => (bool)matchesAnyFilter!.Invoke(null, [path, effective])!)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var excluded = c.InputPaths
                .Where(path => !(bool)matchesAnyFilter!.Invoke(null, [path, effective])!)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.Equal(
                c.ExpectedIncluded.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
                included);
            Assert.Equal(
                c.ExpectedExcluded.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
                excluded);
        }
    }

    private static T LoadFixture<T>(string fileName)
    {
        var path = Path.Combine(GetFixturesDirectory(), fileName);
        Assert.True(File.Exists(path), $"Fixture file not found: '{path}'.");

        var json = File.ReadAllText(path, Encoding.UTF8);
        var value = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(value);
        return value!;
    }

    private static string GetFixturesDirectory()
    {
        // mdz-cli repo root from test output: .../src/mdz.Tests/bin/{cfg}/net10.0
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var siblingSpecRepo = Path.GetFullPath(Path.Combine(
            repoRoot, "..", "markdownzip-spec", "examples", "fixtures"));
        if (Directory.Exists(siblingSpecRepo))
            return siblingSpecRepo;

        var vendoredSpecFixtures = Path.GetFullPath(Path.Combine(
            repoRoot, "spec-fixtures"));
        if (Directory.Exists(vendoredSpecFixtures))
            return vendoredSpecFixtures;

        throw new DirectoryNotFoundException(
            $"Could not locate fixtures. Checked '{siblingSpecRepo}' and '{vendoredSpecFixtures}'.");
    }

    private static void CreateArchiveForEntryPointCase(string archivePath, EntryPointCase c)
    {
        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        foreach (var path in c.Files)
        {
            if (path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var entry = zip.CreateEntry(path);
            using var stream = entry.Open();
            var body = Encoding.UTF8.GetBytes(IsMarkdown(path) ? $"# {path}" : "asset");
            stream.Write(body, 0, body.Length);
        }

        if (c.Manifest is not null)
        {
            var entry = zip.CreateEntry("manifest.json");
            using var stream = entry.Open();
            var json = JsonSerializer.Serialize(new { entryPoint = c.Manifest.EntryPoint });
            var body = Encoding.UTF8.GetBytes(json);
            stream.Write(body, 0, body.Length);
        }
    }

    private static bool IsMarkdown(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private sealed class EntryPointCase
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Files { get; set; } = [];
        public EntryPointManifest? Manifest { get; set; }
        public string? ExpectedEntryPoint { get; set; }
    }

    private sealed class EntryPointManifest
    {
        public string? EntryPoint { get; set; }
    }

    private sealed class PathValidationCase
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool Valid { get; set; }
        public string? ErrorCode { get; set; }
    }

    private sealed class CreateFilterCase
    {
        public string Name { get; set; } = string.Empty;
        public List<string> InputPaths { get; set; } = [];
        public List<string> Filters { get; set; } = [];
        public List<string> ExpectedIncluded { get; set; } = [];
        public List<string> ExpectedExcluded { get; set; } = [];
    }
}
