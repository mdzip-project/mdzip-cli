using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using MDZip.Commands;
using MDZip.Core;

namespace MDZip.Tests;

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
            Assert.True(c.ExpectedEntryPoint == actual, $"{c.Name}: got '{actual}'");

            // 1.2.0: a declared entryPoint that's invalid or missing never falls back (§5.5).
            if (c.ExpectedError is not null)
            {
                var ex = Assert.ThrowsAny<InvalidOperationException>(() => MdzArchive.ResolveEntryPointStrict(archivePath));
                Assert.True(c.ExpectedError == MdzSpec.ErrorCode(ex), $"{c.Name}: {ex.Message}");
            }
        }
    }

    [Fact]
    public void PathValidationCases_MatchFixtures()
    {
        var cases = LoadFixture<List<PathValidationCase>>("path-validation-cases.json");
        Assert.NotNull(cases);

        foreach (var c in cases!)
        {
            var problem = PathValidator.GetProblem(c.Path);
            Assert.True(c.Valid == problem is null, $"{c.Name}: {problem?.Message}");

            if (!c.Valid && !string.IsNullOrWhiteSpace(c.ErrorCode))
                Assert.True(c.ErrorCode == problem!.Code, $"{c.Name}: got {problem!.Code}");
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

    /// <summary>
    /// The vendored copy of the spec's fixtures (spec-fixtures/, synced from a
    /// released spec tag with mdzip-spec tools/sync-fixtures.mjs). Set
    /// MDZIP_SPEC_FIXTURES to a fixtures directory to test against another copy,
    /// for example a draft spec checkout. A sibling mdzip-spec checkout is not
    /// used implicitly, so results don't depend on which branch it's on.
    /// </summary>
    internal static string FixturesDirectory => GetFixturesDirectory();

    private static string GetFixturesDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable("MDZIP_SPEC_FIXTURES");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            if (!Directory.Exists(overrideDir))
                throw new DirectoryNotFoundException($"MDZIP_SPEC_FIXTURES points to '{overrideDir}', which doesn't exist.");
            return overrideDir;
        }

        // mdz-cli repo root from test output: .../src/mdz.Tests/bin/{cfg}/{tfm}
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var vendoredSpecFixtures = Path.GetFullPath(Path.Combine(
            repoRoot, "spec-fixtures"));
        if (Directory.Exists(vendoredSpecFixtures))
            return vendoredSpecFixtures;

        throw new DirectoryNotFoundException(
            $"Could not locate fixtures at '{vendoredSpecFixtures}'. Run: node ../mdzip-spec/tools/sync-fixtures.mjs spec-fixtures --ref <tag>");
    }

    [Fact]
    public void SpecFixtures_MatchMarker()
    {
        var dir = GetFixturesDirectory();
        var markerPath = Path.Combine(dir, "SPEC-FIXTURES.json");
        if (!File.Exists(markerPath))
            return; // an override directory (e.g. a spec checkout) has no marker

        using var marker = JsonDocument.Parse(File.ReadAllText(markerPath));
        var specRef = marker.RootElement.GetProperty("ref").GetString();
        foreach (var file in marker.RootElement.GetProperty("files").EnumerateObject())
        {
            var path = Path.Combine(dir, file.Name);
            Assert.True(File.Exists(path), $"Spec fixture '{file.Name}' is missing; re-run sync-fixtures.mjs.");
            var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            Assert.True(
                actual == file.Value.GetString(),
                $"Spec fixture '{file.Name}' differs from spec {specRef}; re-run sync-fixtures.mjs instead of editing it.");
        }
    }

    private static void CreateArchiveForEntryPointCase(string archivePath, EntryPointCase c)
    {
        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        foreach (var path in c.Files)
        {
            if (path.Equals("manifest.json", StringComparison.Ordinal))
                continue;

            var entry = zip.CreateEntry(path);
            using var stream = entry.Open();
            var body = Encoding.UTF8.GetBytes(IsMarkdown(path) ? $"# {path}" : "asset");
            stream.Write(body, 0, body.Length);
        }

        if (c.ManifestJson is not null)
        {
            // Malformed manifests are given as exact text (see the spec fixtures README).
            var entry = zip.CreateEntry("manifest.json");
            using var stream = entry.Open();
            var body = Encoding.UTF8.GetBytes(c.ManifestJson);
            stream.Write(body, 0, body.Length);
        }
        else if (c.Manifest is not null)
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
        public string? ManifestJson { get; set; }
        public string? ExpectedEntryPoint { get; set; }
        public string? ExpectedError { get; set; }
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
