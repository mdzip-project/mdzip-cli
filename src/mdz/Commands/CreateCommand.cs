using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using Mdz.Core;
using Mdz.Models;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz create' subcommand.
/// Creates a .mdz archive from a source directory or individual files.
/// </summary>
public static class CreateCommand
{
    public static Command Build()
    {
        var sourceArg = new Argument<DirectoryInfo?>(
            name: "source",
            description: "Source directory containing files to package.")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };

        var outputArg = new Argument<FileInfo?>(
            name: "output",
            description: "Path to the output .mdz file to create.");
        outputArg.Arity = ArgumentArity.ZeroOrOne;

        var sourceOption = new Option<DirectoryInfo?>(
            aliases: ["--source", "-s"],
            description: "Required source directory. Can be provided positionally as <source>.");

        var filterOption = new Option<string[]>(
            aliases: ["--filter", "-fi"],
            description: "Glob filter (archive-relative, repeatable). If omitted, defaults to markdown and common image files.");

        var outputOption = new Option<FileInfo?>(
            aliases: ["--output", "-o"],
            description: "Required output archive path. Can be provided positionally as <output>. If no extension is supplied, .mdz is added automatically.");

        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Overwrite the output archive if it already exists.");

        var createIndexOption = new Option<bool>(
            aliases: ["--create-index", "-ci"],
            description: "Automatically create index.md with links to Markdown files when entry-point resolution is ambiguous.");

        var noInteractiveOption = new Option<bool>(
            aliases: ["--no-interactive"],
            description: "Disable interactive prompts. Recommended for CI/build pipelines.");

        var mapFilesOption = new Option<bool>(
            aliases: ["--map-files", "-mf"],
            description: "* Write/update manifest.json files[] mapping (path, originalPath, title). Sanitizes invalid source paths if needed.");

        var titleOption = new Option<string?>(
            aliases: ["--title", "-t"],
            description: "* Document title for manifest.title.");

        var entryPointOption = new Option<string?>(
            aliases: ["--entry-point", "-e"],
            description: "* Relative path for manifest.entryPoint.");

        var languageOption = new Option<string?>(
            aliases: ["--language", "-l"],
            description: "* BCP 47 language tag for manifest.language (e.g. 'en', 'fr-CA'). Defaults to 'en'.");

        var authorOption = new Option<string?>(
            aliases: ["--author", "-a"],
            description: "* Primary author name for manifest.author.name.");

        var descriptionOption = new Option<string?>(
            aliases: ["--description", "-d"],
            description: "* Short description for manifest.description.");

        var versionOption = new Option<string?>(
            aliases: ["--doc-version"],
            description: "* Document version for manifest.version (e.g. '1.0.0').");

        var cmd = new Command(
            "create",
            "Create an .mdz archive from a source directory.\n\nExample:\n  mdz create ./my-doc-folder my-doc.mdz --title \"My Document\"")
        {
            sourceArg,
            outputArg,
            sourceOption,
            filterOption,
            outputOption,
            forceOption,
            createIndexOption,
            noInteractiveOption,
            mapFilesOption,
            titleOption,
            entryPointOption,
            languageOption,
            authorOption,
            descriptionOption,
            versionOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var source = ctx.ParseResult.GetValueForOption(sourceOption) ?? ctx.ParseResult.GetValueForArgument(sourceArg);
            var filters = ctx.ParseResult.GetValueForOption(filterOption) ?? [];
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? ctx.ParseResult.GetValueForArgument(outputArg);
            var force = ctx.ParseResult.GetValueForOption(forceOption);
            var createIndex = ctx.ParseResult.GetValueForOption(createIndexOption);
            var noInteractive = ctx.ParseResult.GetValueForOption(noInteractiveOption);
            var mapFiles = ctx.ParseResult.GetValueForOption(mapFilesOption);
            var title = ctx.ParseResult.GetValueForOption(titleOption);
            var entryPoint = ctx.ParseResult.GetValueForOption(entryPointOption);
            var language = ctx.ParseResult.GetValueForOption(languageOption);
            var author = ctx.ParseResult.GetValueForOption(authorOption);
            var description = ctx.ParseResult.GetValueForOption(descriptionOption);
            var docVersion = ctx.ParseResult.GetValueForOption(versionOption);

            ctx.ExitCode = Handle(output, source, filters, force, createIndex, noInteractive, mapFiles, title, entryPoint, language, author, description, docVersion);
        });

        return cmd;
    }

    private static int Handle(
        FileInfo? output,
        DirectoryInfo? source,
        string[] filters,
        bool force,
        bool createIndex,
        bool noInteractive,
        bool mapFiles,
        string? title,
        string? entryPoint,
        string? language,
        string? author,
        string? description,
        string? docVersion)
    {
        if (source is null)
        {
            Console.Error.WriteLine("Error: Source directory is required. Provide <source> or --source.");
            return 1;
        }

        if (output is null)
        {
            Console.Error.WriteLine("Error: Output file is required. Provide <output> or --output.");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Error.WriteLine(
                    "Hint: This usually means <output> was not parsed as a separate argument. " +
                    "If <source> contains spaces, quote it. Also avoid a trailing '\\' at the end of the source path " +
                    "(for example: mdz create \".\\My Docs\" docs -ci)."
                );
            }
            return 1;
        }

        var outputPath = output.FullName;
        if (!Path.HasExtension(outputPath))
            outputPath += ".mdz";

        if (Directory.Exists(outputPath))
        {
            Console.Error.WriteLine($"Error: Output path '{outputPath}' is an existing directory.");
            return 1;
        }

        if (File.Exists(outputPath) && !force)
        {
            Console.Error.WriteLine($"Error: Output file '{outputPath}' already exists. Use --force to overwrite.");
            return 1;
        }

        if (!source.Exists)
        {
            Console.Error.WriteLine($"Error: Source directory '{source.FullName}' does not exist.");
            return 1;
        }

        var hasManifestOption =
            mapFiles
            || 
            title is not null
            || entryPoint is not null
            || language is not null
            || author is not null
            || description is not null
            || docVersion is not null;

        // Build optional manifest. Any metadata option triggers manifest creation.
        Manifest? manifest = null;
            if (hasManifestOption)
            {
                var effectiveTitle = title;
                if (string.IsNullOrWhiteSpace(effectiveTitle))
                {
                effectiveTitle = source.Name;
                Console.WriteLine($"Info: --title was not provided. Using source folder name '{effectiveTitle}' for manifest title.");
                }

            manifest = new Manifest
            {
                Spec = new ManifestSpec { Name = "markdownzip-spec", Version = "1.0.1-draft" },
                Producer = BuildProducerMetadata(),
                Title = effectiveTitle,
                EntryPoint = entryPoint,
                Language = language ?? "en",
                Description = description,
                Version = docVersion,
                Author = author is not null ? new ManifestAuthor { Name = author } : null,
            };
        }

        try
        {
            var effectiveFilters = GetEffectiveFilters(filters);
            var scan = ScanSourceFiles(source.FullName, manifest, mapFiles, effectiveFilters);
            if (!mapFiles && scan.InvalidPathCount > 0 && IsInteractiveConsole(noInteractive))
            {
                mapFiles = PromptMapFiles(scan.InvalidPathCount);
                if (mapFiles)
                {
                    Console.WriteLine("Re-scanning with file mapping enabled.");
                    EnsureManifestForFileMapping(ref manifest, source.Name, title);
                    scan = ScanSourceFiles(source.FullName, manifest, mapFiles: true, effectiveFilters);
                }
            }

            if (mapFiles)
                EnsureManifestForFileMapping(ref manifest, source.Name, title);

            if (mapFiles && manifest is not null)
                manifest.Files = scan.ManifestFiles;

            if (createIndex)
            {
                var files = scan.Files.ToList();

                var entryPointResolved = ResolveEntryPoint(files.Select(f => f.ArchivePath).ToList(), manifest);
                var tempIndexPath = string.Empty;

                if (entryPointResolved is null)
                {
                    if (!string.IsNullOrWhiteSpace(manifest?.EntryPoint))
                    {
                        Console.Error.WriteLine(
                            $"Error: Manifest entry-point '{manifest.EntryPoint}' was provided but does not exist. " +
                            "Update --entry-point or remove it to allow --create-index.");
                        return 1;
                    }

                    var markdownPaths = files
                        .Select(f => f.ArchivePath)
                        .Where(IsMarkdownFile)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    tempIndexPath = Path.Combine(Path.GetTempPath(), $"mdz-index-{Guid.NewGuid():N}.md");
                    var createCommand = BuildCreateCommandForFooter(
                        sourcePath: source.FullName,
                        outputPath: outputPath,
                        filters: effectiveFilters,
                        force: force,
                        createIndex: createIndex,
                        mapFiles: mapFiles,
                        title: title,
                        entryPoint: entryPoint,
                        language: language,
                        author: author,
                        description: description,
                        docVersion: docVersion);
                    File.WriteAllText(
                        tempIndexPath,
                        BuildGeneratedIndex(markdownPaths, createCommand, title),
                        Encoding.UTF8);
                    files.Add(("index.md", tempIndexPath));
                    Console.WriteLine("Added generated archive entry 'index.md'.");

                    if (manifest is not null)
                        manifest.EntryPoint = "index.md";
                }

                try
                {
                    MdzArchive.CreateFromFiles(outputPath, files, manifest);
                    WriteCreateSummary(files.Count, scan);
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tempIndexPath) && File.Exists(tempIndexPath))
                        File.Delete(tempIndexPath);
                }
            }
            else
            {
                var archivePaths = scan.Files.Select(file => file.ArchivePath).ToList();
                if (string.IsNullOrWhiteSpace(manifest?.EntryPoint)
                    && ResolveEntryPoint(archivePaths, manifest) is null
                    && IsInteractiveConsole(noInteractive))
                {
                    createIndex = PromptCreateIndex();
                    if (createIndex)
                        Console.WriteLine("Generating default index.md.");
                }

                if (createIndex)
                {
                    var files = scan.Files.ToList();
                    var markdownPaths = files
                        .Select(f => f.ArchivePath)
                        .Where(IsMarkdownFile)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var tempIndexPath = Path.Combine(Path.GetTempPath(), $"mdz-index-{Guid.NewGuid():N}.md");
                    var createCommand = BuildCreateCommandForFooter(
                        sourcePath: source.FullName,
                        outputPath: outputPath,
                        filters: effectiveFilters,
                        force: force,
                        createIndex: createIndex,
                        mapFiles: mapFiles,
                        title: title,
                        entryPoint: entryPoint,
                        language: language,
                        author: author,
                        description: description,
                        docVersion: docVersion);
                    File.WriteAllText(
                        tempIndexPath,
                        BuildGeneratedIndex(markdownPaths, createCommand, title),
                        Encoding.UTF8);
                    files.Add(("index.md", tempIndexPath));
                    Console.WriteLine("Added generated archive entry 'index.md'.");

                    if (manifest is not null)
                        manifest.EntryPoint = "index.md";

                    try
                    {
                        MdzArchive.CreateFromFiles(outputPath, files, manifest);
                        WriteCreateSummary(files.Count, scan);
                    }
                    finally
                    {
                        if (File.Exists(tempIndexPath))
                            File.Delete(tempIndexPath);
                    }
                }
                else
                {
                    MdzArchive.CreateFromFiles(outputPath, scan.Files, manifest);
                    WriteCreateSummary(scan.Files.Count, scan);
                }
            }

            Console.WriteLine($"Created '{outputPath}'");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Archive was not created.");
            if (File.Exists(outputPath))
            {
                Console.Error.WriteLine(
                    $"Note: Existing file '{outputPath}' was left unchanged and may be from an earlier run.");
            }
            return 1;
        }
    }

    private static string BuildGeneratedIndex(
        IReadOnlyList<string> markdownPaths,
        string createCommand,
        string? title)
    {
        var sb = new StringBuilder();
        var pageTitle = string.IsNullOrWhiteSpace(title) ? "Index" : title;
        sb.AppendLine($"# {pageTitle}");
        sb.AppendLine();

        if (markdownPaths.Count == 0)
        {
            sb.AppendLine("No Markdown files were found.");
            return sb.ToString();
        }

        var root = new IndexNode(string.Empty, isDirectory: true);
        foreach (var path in markdownPaths)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var isDirectory = i < parts.Length - 1;
                current = current.GetOrAdd(parts[i], isDirectory);
            }
        }

        RenderIndexTree(root, sb, parentPath: string.Empty, indent: 0);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"Generated by `{createCommand}`");
        sb.AppendLine();
        sb.AppendLine("More info: [markdownzip.org](https://markdownzip.org)");

        return sb.ToString();
    }

    private static string BuildCreateCommandForFooter(
        string sourcePath,
        string outputPath,
        IReadOnlyList<string> filters,
        bool force,
        bool createIndex,
        bool mapFiles,
        string? title,
        string? entryPoint,
        string? language,
        string? author,
        string? description,
        string? docVersion)
    {
        var parts = new List<string>
        {
            "mdz",
            "create",
            QuoteArgIfNeeded(sourcePath),
            QuoteArgIfNeeded(outputPath),
        };

        foreach (var filter in filters)
        {
            parts.Add("--filter");
            parts.Add(QuoteArgIfNeeded(filter));
        }

        if (force)
            parts.Add("--force");
        if (createIndex)
            parts.Add("--create-index");
        if (mapFiles)
            parts.Add("--map-files");

        AppendOption(parts, "--title", title);
        AppendOption(parts, "--entry-point", entryPoint);
        AppendOption(parts, "--language", language);
        AppendOption(parts, "--author", author);
        AppendOption(parts, "--description", description);
        AppendOption(parts, "--doc-version", docVersion);

        return string.Join(' ', parts);
    }

    private static void AppendOption(List<string> parts, string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        parts.Add(option);
        parts.Add(QuoteArgIfNeeded(value));
    }

    private static string QuoteArgIfNeeded(string value)
    {
        if (value.Contains(' ') || value.Contains('\t') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\\\"")}\"";
        return value;
    }

    private static void RenderIndexTree(IndexNode node, StringBuilder sb, string parentPath, int indent)
    {
        var orderedChildren = node.Children
            .OrderByDescending(c => c.IsDirectory)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var child in orderedChildren)
        {
            var currentPath = string.IsNullOrEmpty(parentPath)
                ? child.Name
                : $"{parentPath}/{child.Name}";
            var indentText = new string(' ', indent * 2);

            if (child.IsDirectory)
            {
                sb.AppendLine($"{indentText}- {child.Name}/");
                RenderIndexTree(child, sb, currentPath, indent + 1);
            }
            else
            {
                var linkTarget = ToMarkdownLinkTarget(currentPath);
                sb.AppendLine($"{indentText}- [{child.Name}]({linkTarget})");
            }
        }
    }

    private static string? ResolveEntryPoint(IReadOnlyList<string> archivePaths, Manifest? manifest)
    {
        if (manifest?.EntryPoint is { Length: > 0 } ep
            && archivePaths.Any(path => path.Equals(ep, StringComparison.OrdinalIgnoreCase)))
        {
            return ep;
        }

        if (archivePaths.Any(path => path.Equals("index.md", StringComparison.OrdinalIgnoreCase)))
            return "index.md";

        var rootMarkdown = archivePaths
            .Where(path => !path.Contains('/'))
            .Where(IsMarkdownFile)
            .ToList();

        return rootMarkdown.Count == 1 ? rootMarkdown[0] : null;
    }

    private static bool IsMarkdownFile(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private static string ToMarkdownLinkTarget(string path)
    {
        var encoded = string.Join('/',
            path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        return $"<{encoded}>";
    }

    private static bool IsInteractiveConsole(bool noInteractive)
    {
        if (noInteractive)
            return false;

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            return false;

        // Common CI providers set CI=true/1; treat that as non-interactive.
        var ci = Environment.GetEnvironmentVariable("CI");
        if (!string.IsNullOrWhiteSpace(ci)
            && !ci.Equals("false", StringComparison.OrdinalIgnoreCase)
            && !ci.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static SourceScanResult ScanSourceFiles(
        string sourceDirectory,
        Manifest? manifest,
        bool mapFiles,
        IReadOnlyList<string> filters)
    {
        var files = new List<(string ArchivePath, string LocalPath)>();
        var manifestFiles = new List<ManifestFile>();
        var skippedByReason = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var usedArchivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var invalidPathCount = 0;
        var sanitizedPathCount = 0;

        foreach (var localPath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var originalPath = Path.GetRelativePath(sourceDirectory, localPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var archivePath = originalPath;

            if (!MatchesAnyFilter(originalPath, filters))
            {
                AddSkip(skippedByReason, "excluded by filter");
                continue;
            }

            if (manifest is not null && archivePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                AddSkip(skippedByReason, "manifest.json replaced by generated manifest");
                continue;
            }

            var pathError = PathValidator.Validate(archivePath);
            if (pathError is not null)
            {
                invalidPathCount++;
                if (!mapFiles)
                {
                    AddSkip(skippedByReason, "invalid path for MDZ rules");
                    continue;
                }

                archivePath = MakeUniqueArchivePath(SanitiseArchivePath(archivePath), usedArchivePaths);
                sanitizedPathCount++;
            }
            else
            {
                archivePath = MakeUniqueArchivePath(archivePath, usedArchivePaths);
            }

            if (!CanRead(localPath))
            {
                AddSkip(skippedByReason, "unreadable/locked file");
                continue;
            }

            files.Add((archivePath, localPath));

            if (mapFiles && IsMarkdownFile(archivePath))
            {
                manifestFiles.Add(new ManifestFile
                {
                    Path = archivePath,
                    OriginalPath = originalPath,
                    Title = BuildDisplayTitleFromPath(originalPath),
                });
            }
        }

        return new SourceScanResult(files, manifestFiles, skippedByReason, invalidPathCount, sanitizedPathCount);
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void AddSkip(Dictionary<string, int> skippedByReason, string reason)
    {
        skippedByReason.TryGetValue(reason, out var existing);
        skippedByReason[reason] = existing + 1;
    }

    private static void WriteCreateSummary(int addedFiles, SourceScanResult scan)
    {
        Console.WriteLine($"{addedFiles} file(s) added to archive.");
        if (scan.SanitizedPathCount > 0)
            Console.WriteLine($"{scan.SanitizedPathCount} file(s) had invalid paths and were sanitized.");

        if (scan.SkippedCount == 0)
            return;

        var reasonSummary = string.Join(
            "; ",
            scan.SkippedByReason
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => FormatReason(kvp.Value, kvp.Key)));
        Console.WriteLine($"{scan.SkippedCount} file(s) skipped: {reasonSummary}.");
    }

    private static string FormatReason(int count, string reason)
    {
        if (count == 1)
            return $"1 {reason}";

        return reason.EndsWith("file", StringComparison.OrdinalIgnoreCase)
            ? $"{count} {reason}s"
            : $"{count} {reason}";
    }

    private static void EnsureManifestForFileMapping(ref Manifest? manifest, string sourceName, string? requestedTitle)
    {
        if (manifest is not null)
            return;

        var effectiveTitle = requestedTitle;
        if (string.IsNullOrWhiteSpace(effectiveTitle))
        {
            effectiveTitle = sourceName;
            Console.WriteLine($"Info: --title was not provided. Using source folder name '{effectiveTitle}' for manifest title.");
        }

        manifest = new Manifest
        {
            Spec = new ManifestSpec { Name = "markdownzip-spec", Version = "1.0.1-draft" },
            Producer = BuildProducerMetadata(),
            Title = effectiveTitle,
            Language = "en",
        };
    }

    private static ManifestProducer BuildProducerMetadata()
    {
        var appAssembly = Assembly.GetEntryAssembly() ?? typeof(CreateCommand).Assembly;
        var appVersion = GetInformationalVersion(appAssembly);
        var coreVersion = GetInformationalVersion(typeof(MdzArchive).Assembly);

        return new ManifestProducer
        {
            Application = new ManifestAgent
            {
                Name = "mdz-cli",
                Version = appVersion,
                Url = "https://github.com/kylemwhite/mdz-cli",
            },
            Core = new ManifestAgent
            {
                Name = "mdz-core",
                Version = coreVersion,
                Url = "https://github.com/kylemwhite/mdz-core",
            },
        };
    }

    private static string? GetInformationalVersion(Assembly assembly)
    {
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+')[0];

        return assembly.GetName().Version?.ToString();
    }

    private static string SanitiseArchivePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var safeSegments = segments
            .Select(SanitisePathSegment)
            .ToArray();
        return string.Join('/', safeSegments);
    }

    private static string SanitisePathSegment(string segment)
    {
        var sb = new StringBuilder(segment.Length);
        foreach (var c in segment)
        {
            if (c is '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' || c == '\0' || (c >= '\u0001' && c <= '\u001F') || c == '\u007F')
                sb.Append('_');
            else
                sb.Append(c);
        }

        var sanitized = sb.ToString().Trim();
        if (string.IsNullOrEmpty(sanitized))
            return "_";
        if (sanitized == "." || sanitized == "..")
            return sanitized.Replace('.', '_');
        return sanitized;
    }

    private static string MakeUniqueArchivePath(string candidate, HashSet<string> usedPaths)
    {
        if (usedPaths.Add(candidate))
            return candidate;

        var directory = Path.GetDirectoryName(candidate)?.Replace('\\', '/');
        var fileName = Path.GetFileName(candidate);
        var extension = Path.GetExtension(fileName);
        var baseName = fileName[..^extension.Length];

        var counter = 2;
        while (true)
        {
            var suffixed = $"{baseName}-{counter}{extension}";
            var next = string.IsNullOrEmpty(directory) ? suffixed : $"{directory}/{suffixed}";
            if (usedPaths.Add(next))
                return next;
            counter++;
        }
    }

    private static string BuildDisplayTitleFromPath(string originalPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(originalPath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName))
            return originalPath;

        var spaced = fileName.Replace('_', ' ').Replace('-', ' ');
        return string.Join(' ', spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool PromptCreateIndex()
    {
        Console.Write("No unambiguous entry point found. Create a default index.md now? [y/N]: ");
        var response = Console.ReadLine()?.Trim();
        return response is not null
            && (response.Equals("y", StringComparison.OrdinalIgnoreCase)
                || response.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool PromptMapFiles(int invalidPathCount)
    {
        Console.Write($"Detected {invalidPathCount} file(s) with invalid archive path characters. Enable --map-files to sanitize and map them in manifest.json? [y/N]: ");
        var response = Console.ReadLine()?.Trim();
        return response is not null
            && (response.Equals("y", StringComparison.OrdinalIgnoreCase)
                || response.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetEffectiveFilters(string[] filters)
    {
        if (filters.Length > 0)
            return filters;

        return
        [
            "**/*.md",
            "**/*.markdown",
            "**/*.png",
            "**/*.jpg",
            "**/*.jpeg",
            "**/*.gif",
            "**/*.webp",
            "**/*.svg",
            "**/*.avif",
        ];
    }

    private static bool MatchesAnyFilter(string path, IReadOnlyList<string> filters)
    {
        foreach (var filter in filters)
        {
            if (GlobMatch(path, filter))
                return true;
        }

        return false;
    }

    private static bool GlobMatch(string path, string pattern)
    {
        var pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var patternParts = pattern.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return GlobMatchParts(pathParts, 0, patternParts, 0);
    }

    private static bool GlobMatchParts(string[] pathParts, int pi, string[] patternParts, int gi)
    {
        if (gi == patternParts.Length)
            return pi == pathParts.Length;

        if (patternParts[gi] == "**")
        {
            if (gi == patternParts.Length - 1)
                return true;

            for (var skip = pi; skip <= pathParts.Length; skip++)
            {
                if (GlobMatchParts(pathParts, skip, patternParts, gi + 1))
                    return true;
            }

            return false;
        }

        if (pi >= pathParts.Length)
            return false;

        return SegmentMatch(pathParts[pi], patternParts[gi])
            && GlobMatchParts(pathParts, pi + 1, patternParts, gi + 1);
    }

    private static bool SegmentMatch(string segment, string pattern)
    {
        var si = 0;
        var pi = 0;
        var star = -1;
        var match = 0;

        while (si < segment.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == '?' || char.ToLowerInvariant(pattern[pi]) == char.ToLowerInvariant(segment[si])))
            {
                si++;
                pi++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                star = pi++;
                match = si;
            }
            else if (star != -1)
            {
                pi = star + 1;
                si = ++match;
            }
            else
            {
                return false;
            }
        }

        while (pi < pattern.Length && pattern[pi] == '*')
            pi++;

        return pi == pattern.Length;
    }

    private sealed class IndexNode(string name, bool isDirectory)
    {
        public string Name { get; } = name;
        public bool IsDirectory { get; private set; } = isDirectory;
        public List<IndexNode> Children { get; } = [];

        public IndexNode GetOrAdd(string name, bool isDirectory)
        {
            var existing = Children.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.IsDirectory |= isDirectory;
                return existing;
            }

            var created = new IndexNode(name, isDirectory);
            Children.Add(created);
            return created;
        }
    }

    private sealed record SourceScanResult(
        List<(string ArchivePath, string LocalPath)> Files,
        List<ManifestFile> ManifestFiles,
        Dictionary<string, int> SkippedByReason,
        int InvalidPathCount,
        int SanitizedPathCount)
    {
        public int SkippedCount => SkippedByReason.Values.Sum();
    }

}
