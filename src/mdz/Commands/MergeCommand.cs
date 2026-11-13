using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MDZip.Commands;

/// <summary>
/// <c>mdz merge &lt;base&gt; &lt;ours&gt; &lt;theirs&gt;</c>: a Git merge driver for <c>.mdz</c> files
/// (mdzip-cli#14). Git can't merge a ZIP, so without this two branches that change the same archive
/// always conflict and one side's edits are redone by hand. This merges the archives entry by entry:
/// <list type="bullet">
/// <item><b>Text entries</b> (Markdown, SVG, CSV, ...) are three-way merged with
/// <c>git merge-file --diff3</c>, as Git merges code. Edits to different parts merge cleanly;
/// overlapping edits get conflict markers (with the base version) inside that entry, and the archive
/// stays valid.</item>
/// <item><b>Binary entries</b> (images): the side that changed wins. When both changed the same
/// entry, ours is kept and theirs is added beside it as <c>name.theirs.ext</c>, so nothing is
/// lost.</item>
/// <item><b>Added and removed entries</b> are applied. An entry one side deleted and the other
/// changed is kept, changed.</item>
/// <item><b><c>manifest.json</c></b> is merged field by field and stays valid JSON: <c>modified</c>
/// takes the later time and <c>created</c> the earlier, and a field both sides set differently
/// keeps ours.</item>
/// </list>
/// The merged archive replaces <c>ours</c>. Exit code 0 is a clean merge; 1 means something is left
/// to resolve (each item is reported on stderr), so Git marks the file as conflicted; 2 means the
/// archives couldn't be read, and <c>ours</c> is left as it was.
/// </summary>
public static class MergeCommand
{
    private const string ManifestPath = "manifest.json";

    /// <summary>The git executable that runs <c>git merge-file</c>. Tests may point it elsewhere.</summary>
    public static string GitExecutable { get; set; } = "git";

    public static Command Build()
    {
        var baseArg = new Argument<FileInfo>("base", "The common ancestor (Git's %O). May be empty when both sides added the file.");
        var oursArg = new Argument<FileInfo>("ours", "Our version (Git's %A). The merged archive is written here.");
        var theirsArg = new Argument<FileInfo>("theirs", "Their version (Git's %B).");
        var markerSizeOption = new Option<int>("--marker-size", () => 7, "Length of conflict markers (Git's %L).");
        var pathOption = new Option<string?>("--path", "The archive's path in the repository (Git's %P), for messages.");
        var oursLabelOption = new Option<string>("--ours-label", () => "ours", "Label on our side of conflict markers.");
        var baseLabelOption = new Option<string>("--base-label", () => "base", "Label on the base section of conflict markers.");
        var theirsLabelOption = new Option<string>("--theirs-label", () => "theirs", "Label on their side of conflict markers.");

        var cmd = new Command("merge",
            "Merge two versions of an archive with their common ancestor, for Git merges. " +
            "Set up with: git config merge.mdz.name \"MDZip archive merge\", " +
            "git config merge.mdz.driver \"mdz merge %O %A %B --marker-size %L --path %P\" " +
            "and '*.mdz merge=mdz' in .gitattributes.")
        {
            baseArg, oursArg, theirsArg, markerSizeOption, pathOption, oursLabelOption, baseLabelOption, theirsLabelOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var parse = ctx.ParseResult;
            var options = new MergeOptions(
                MarkerSize: Math.Max(1, parse.GetValueForOption(markerSizeOption)),
                DisplayPath: parse.GetValueForOption(pathOption),
                OursLabel: parse.GetValueForOption(oursLabelOption) ?? "ours",
                BaseLabel: parse.GetValueForOption(baseLabelOption) ?? "base",
                TheirsLabel: parse.GetValueForOption(theirsLabelOption) ?? "theirs");
            var result = Merge(
                parse.GetValueForArgument(baseArg)!.FullName,
                parse.GetValueForArgument(oursArg)!.FullName,
                parse.GetValueForArgument(theirsArg)!.FullName,
                options);
            foreach (var line in result.Messages)
                Console.Error.WriteLine(line);
            ctx.ExitCode = result.ExitCode;
        });

        return cmd;
    }

    public sealed record MergeOptions(
        int MarkerSize = 7,
        string? DisplayPath = null,
        string OursLabel = "ours",
        string BaseLabel = "base",
        string TheirsLabel = "theirs");

    /// <summary>What a merge did: 0 clean, 1 conflicts left (listed in <see cref="Messages"/>), 2 not merged.</summary>
    public sealed record MergeResult(int ExitCode, IReadOnlyList<string> Messages, IReadOnlyList<string> Conflicts);

    private sealed record Entry(string Path, byte[] Bytes, DateTimeOffset LastWriteTime);

    /// <summary>Merges <paramref name="theirsPath"/> into <paramref name="oursPath"/> with <paramref name="basePath"/> as the ancestor.</summary>
    public static MergeResult Merge(string basePath, string oursPath, string theirsPath, MergeOptions? options = null)
    {
        options ??= new MergeOptions();
        var name = options.DisplayPath ?? Path.GetFileName(oursPath);
        var messages = new List<string>();

        Dictionary<string, Entry> baseEntries, oursEntries, theirsEntries;
        List<string> oursOrder, theirsOrder;
        try
        {
            (baseEntries, _) = ReadEntries(basePath, allowEmpty: true);
            (oursEntries, oursOrder) = ReadEntries(oursPath, allowEmpty: false);
            (theirsEntries, theirsOrder) = ReadEntries(theirsPath, allowEmpty: false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            messages.Add($"mdz merge: {name}: not merged: {ex.Message}");
            messages.Add($"mdz merge: {name}: keep one version with 'git checkout --ours' or 'git checkout --theirs'.");
            return new MergeResult(2, messages, []);
        }

        var conflicts = new List<string>();
        var merged = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var order = new List<string>();
        void Put(Entry entry)
        {
            if (!merged.ContainsKey(entry.Path)) order.Add(entry.Path);
            merged[entry.Path] = entry;
        }

        // Ours' order first, then what only theirs has, in theirs' order.
        var paths = oursOrder.Concat(theirsOrder.Where(p => !oursEntries.ContainsKey(p)))
            .Concat(baseEntries.Keys.Where(p => !oursEntries.ContainsKey(p) && !theirsEntries.ContainsKey(p)))
            .Where(p => p != ManifestPath)
            .ToList();

        using var scratch = new ScratchDirectory();
        foreach (var path in paths)
        {
            baseEntries.TryGetValue(path, out var b);
            oursEntries.TryGetValue(path, out var o);
            theirsEntries.TryGetValue(path, out var t);

            if (Same(o, t)) { if (o is not null) Put(o); continue; }
            if (Same(o, b)) { if (t is not null) Put(t); continue; }
            if (Same(t, b)) { if (o is not null) Put(o); continue; }

            // Both sides changed this entry, differently.
            if (o is null || t is null)
            {
                var kept = (o ?? t)!;
                Put(kept);
                var (deleted, changed) = o is null ? (options.OursLabel, options.TheirsLabel) : (options.TheirsLabel, options.OursLabel);
                conflicts.Add($"{path}: deleted in {deleted} and changed in {changed}; kept the changed version.");
                continue;
            }

            if (IsText(path, b, o, t))
            {
                var text = MergeText(scratch, path, b?.Bytes ?? [], o.Bytes, t.Bytes, options);
                if (text is not null)
                {
                    Put(o with { Bytes = text.Value.Bytes });
                    if (text.Value.Conflicts > 0)
                        conflicts.Add($"{path}: {text.Value.Conflicts} conflict{(text.Value.Conflicts == 1 ? "" : "s")} marked in the text.");
                    continue;
                }
            }

            // Binary (or text git couldn't merge): keep ours, and theirs beside it.
            Put(o);
            var copy = FreePath(TheirsCopyPath(path), p => merged.ContainsKey(p) || oursEntries.ContainsKey(p) || theirsEntries.ContainsKey(p));
            Put(t with { Path = copy });
            conflicts.Add($"{path}: changed on both sides; kept {options.OursLabel}, and {options.TheirsLabel}'s version is in {copy}.");
        }

        var manifest = MergeManifest(baseEntries, oursEntries, theirsEntries, options, conflicts);

        try
        {
            WriteArchive(oursPath, manifest, order.Select(p => merged[p]));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            messages.Add($"mdz merge: {name}: not merged: couldn't write the result: {ex.Message}");
            return new MergeResult(2, messages, conflicts);
        }

        if (conflicts.Count == 0)
            return new MergeResult(0, messages, conflicts);

        messages.Add($"mdz merge: {name}: {conflicts.Count} conflict{(conflicts.Count == 1 ? "" : "s")} to resolve:");
        messages.AddRange(conflicts.Select(c => "  " + c));
        return new MergeResult(1, messages, conflicts);
    }

    // ---------------------------------------------------------------- archive I/O

    private static (Dictionary<string, Entry> Entries, List<string> Order) ReadEntries(string archivePath, bool allowEmpty)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var order = new List<string>();
        // Git passes an empty file as the ancestor when both sides added the archive.
        if (allowEmpty && new FileInfo(archivePath) is { Exists: true, Length: 0 })
            return (entries, order);

        using var zip = ZipFile.OpenRead(archivePath);
        foreach (var zipEntry in zip.Entries)
        {
            if (zipEntry.FullName.EndsWith('/')) continue; // directory entries are optional (§5.4)
            var path = zipEntry.FullName.Normalize(NormalizationForm.FormC);
            using var stream = zipEntry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            if (entries.TryAdd(path, new Entry(path, buffer.ToArray(), zipEntry.LastWriteTime)))
                order.Add(path);
        }
        return (entries, order);
    }

    /// <summary>Writes manifest.json first, then the entries, to a temporary file that then replaces <paramref name="outputPath"/>.</summary>
    private static void WriteArchive(string outputPath, Entry? manifest, IEnumerable<Entry> entries)
    {
        var temp = outputPath + ".mdz-merge.tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (var entry in manifest is null ? entries : entries.Prepend(manifest))
                {
                    var zipEntry = zip.CreateEntry(entry.Path, CompressionLevel.Optimal);
                    zipEntry.LastWriteTime = entry.LastWriteTime;
                    using var stream = zipEntry.Open();
                    stream.Write(entry.Bytes);
                }
            }
            File.Move(temp, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static bool Same(Entry? a, Entry? b) =>
        a is null ? b is null : b is not null && a.Bytes.AsSpan().SequenceEqual(b.Bytes);

    // ---------------------------------------------------------------- text

    private static bool IsText(string path, params Entry?[] versions) =>
        TextconvCommand.IsTextPath(path)
        && versions.All(v => v is null || Array.IndexOf(v.Bytes, (byte)0) < 0);

    /// <summary>
    /// Three-way merges one text entry with <c>git merge-file --diff3</c>. Returns the merged bytes and
    /// the number of conflicts, or null when git couldn't be run.
    /// </summary>
    private static (byte[] Bytes, int Conflicts)? MergeText(
        ScratchDirectory scratch, string path, byte[] baseBytes, byte[] oursBytes, byte[] theirsBytes, MergeOptions options)
    {
        var oursFile = scratch.Write("ours", oursBytes);
        var baseFile = scratch.Write("base", baseBytes);
        var theirsFile = scratch.Write("theirs", theirsBytes);

        var start = new ProcessStartInfo(GitExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
        {
            "merge-file", "-p", "--diff3", $"--marker-size={options.MarkerSize}",
            "-L", $"{options.OursLabel}:{path}", "-L", $"{options.BaseLabel}:{path}", "-L", $"{options.TheirsLabel}:{path}",
            oursFile, baseFile, theirsFile,
        })
            start.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(start);
            if (process is null) return null;
            using var output = new MemoryStream();
            var stderr = process.StandardError.ReadToEndAsync();
            process.StandardOutput.BaseStream.CopyTo(output);
            process.WaitForExit();
            _ = stderr.Result;
            // The exit code is the number of conflicts (capped at 127); higher means an error.
            if (process.ExitCode < 0 || process.ExitCode > 127) return null;
            return (output.ToArray(), process.ExitCode);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null; // git isn't on the PATH
        }
    }

    // ---------------------------------------------------------------- manifest

    private static Entry? MergeManifest(
        Dictionary<string, Entry> baseEntries,
        Dictionary<string, Entry> oursEntries,
        Dictionary<string, Entry> theirsEntries,
        MergeOptions options,
        List<string> conflicts)
    {
        baseEntries.TryGetValue(ManifestPath, out var b);
        oursEntries.TryGetValue(ManifestPath, out var o);
        theirsEntries.TryGetValue(ManifestPath, out var t);
        if (Same(o, t)) return o;
        if (Same(o, b)) return t;
        if (Same(t, b)) return o;
        if (o is null || t is null) return o ?? t;

        var baseJson = ParseJson(b);
        var oursJson = ParseJson(o);
        var theirsJson = ParseJson(t);
        if (oursJson is not JsonObject || theirsJson is not JsonObject)
        {
            conflicts.Add($"{ManifestPath}: changed on both sides and not readable JSON; kept {options.OursLabel}.");
            return o;
        }

        var merged = MergeJson(baseJson, oursJson, theirsJson, "", options, conflicts);
        if (merged is not null && JsonNode.DeepEquals(merged, oursJson))
            return o; // keep ours' formatting byte for byte
        var text = merged?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";
        return o with { Bytes = Encoding.UTF8.GetBytes(text + "\n") };
    }

    private static JsonNode? ParseJson(Entry? entry)
    {
        if (entry is null) return null;
        try { return JsonNode.Parse(entry.Bytes); }
        catch (JsonException) { return null; }
    }

    /// <summary>Three-way merge of JSON values. Null means the value is absent.</summary>
    private static JsonNode? MergeJson(
        JsonNode? b, JsonNode? o, JsonNode? t, string where, MergeOptions options, List<string> conflicts)
    {
        if (JsonNode.DeepEquals(o, t)) return o?.DeepClone();
        if (JsonNode.DeepEquals(o, b)) return t?.DeepClone();
        if (JsonNode.DeepEquals(t, b)) return o?.DeepClone();

        if (o is JsonObject oursObject && t is JsonObject theirsObject)
        {
            var baseObject = b as JsonObject;
            var result = new JsonObject();
            var keys = oursObject.Select(p => p.Key)
                .Concat(theirsObject.Select(p => p.Key).Where(k => !oursObject.ContainsKey(k)));
            foreach (var key in keys)
            {
                var value = MergeJson(
                    baseObject?[key], oursObject[key], theirsObject[key],
                    where.Length == 0 ? key : $"{where}.{key}", options, conflicts);
                // A JSON null counts as absent.
                if (value is not null) result[key] = value;
            }
            return result;
        }

        // Timestamps both sides rewrote (re-packing sets them): the archive was
        // created at the earlier time and last modified at the later one.
        if (where == "modified" && PickTime(o, t, later: true) is { } modified)
            return modified.DeepClone();
        if (where == "created" && PickTime(o, t, later: false) is { } created)
            return created.DeepClone();

        // A list of entries keyed by path (the manifest's "files"): merge item by item.
        if (o is JsonArray oursArray && t is JsonArray theirsArray && KeyedByPath(oursArray) && KeyedByPath(theirsArray)
            && (b is null || b is JsonArray baseArray && KeyedByPath(baseArray)))
        {
            return MergeKeyedArray(b as JsonArray, oursArray, theirsArray, where, options, conflicts);
        }

        conflicts.Add($"{ManifestPath}: \"{where}\" changed on both sides; kept {options.OursLabel} ({Describe(o)}), not {options.TheirsLabel} ({Describe(t)}).");
        return o?.DeepClone();
    }

    private static JsonNode? PickTime(JsonNode? o, JsonNode? t, bool later)
    {
        static DateTimeOffset? Parse(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
                ? time : null;
        var ours = Parse(o);
        var theirs = Parse(t);
        if (ours is null || theirs is null) return null;
        return (later ? theirs > ours : theirs < ours) ? t : o;
    }

    private static bool KeyedByPath(JsonArray array) =>
        array.All(item => item is JsonObject obj && obj["path"] is JsonValue path && path.TryGetValue<string>(out _))
        && array.Select(PathOf).Distinct(StringComparer.Ordinal).Count() == array.Count;

    private static string PathOf(JsonNode? item) => item!["path"]!.GetValue<string>();

    private static JsonArray MergeKeyedArray(
        JsonArray? b, JsonArray o, JsonArray t, string where, MergeOptions options, List<string> conflicts)
    {
        var baseItems = (b ?? []).ToDictionary(PathOf, item => item);
        var oursItems = o.ToDictionary(PathOf, item => item);
        var theirsItems = t.ToDictionary(PathOf, item => item);
        var keys = o.Select(PathOf).Concat(t.Select(PathOf).Where(k => !oursItems.ContainsKey(k)))
            .Concat(baseItems.Keys.Where(k => !oursItems.ContainsKey(k) && !theirsItems.ContainsKey(k)));
        var result = new JsonArray();
        foreach (var key in keys)
        {
            var value = MergeJson(
                baseItems.GetValueOrDefault(key), oursItems.GetValueOrDefault(key), theirsItems.GetValueOrDefault(key),
                $"{where}[{key}]", options, conflicts);
            if (value is not null) result.Add(value);
        }
        return result;
    }

    private static string Describe(JsonNode? node)
    {
        if (node is null) return "removed";
        var text = node.ToJsonString();
        return text.Length <= 60 ? text : text[..57] + "...";
    }

    // ---------------------------------------------------------------- helpers

    /// <summary><c>images/chart.png</c> → <c>images/chart.theirs.png</c>.</summary>
    private static string TheirsCopyPath(string path)
    {
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash + 1 ? $"{path[..dot]}.theirs{path[dot..]}" : $"{path}.theirs";
    }

    private static string FreePath(string path, Func<string, bool> taken)
    {
        if (!taken(path)) return path;
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        var (stem, extension) = dot > slash + 1 ? (path[..dot], path[dot..]) : (path, "");
        for (var n = 2; ; n++)
        {
            var candidate = $"{stem}-{n}{extension}";
            if (!taken(candidate)) return candidate;
        }
    }

    private sealed class ScratchDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "mdz-merge-" + Guid.NewGuid().ToString("N"));

        public string Write(string name, byte[] bytes)
        {
            Directory.CreateDirectory(_path);
            var file = Path.Combine(_path, name);
            File.WriteAllBytes(file, bytes);
            return file;
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_path)) Directory.Delete(_path, recursive: true); }
            catch (IOException) { }
        }
    }
}
