using System.CommandLine;
using System.CommandLine.Invocation;
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
            description: "Source directory containing the files to package.")
        {
            Arity = ArgumentArity.ZeroOrOne,
        };

        var outputArg = new Argument<FileInfo?>(
            name: "output",
            description: "Path to the output .mdz file to create.");
        outputArg.Arity = ArgumentArity.ZeroOrOne;

        var sourceOption = new Option<DirectoryInfo?>(
            aliases: ["--source", "-s"],
            description: "Source directory containing the files to package.");

        var outputOption = new Option<FileInfo?>(
            aliases: ["--output", "-o"],
            description: "Path to the output .mdz file to create.");

        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Overwrite the output archive if it already exists.");

        var createIndexOption = new Option<bool>(
            aliases: ["--create-index"],
            description: "Automatically create index.md with links to Markdown files when entry-point resolution is ambiguous.");

        var titleOption = new Option<string?>(
            aliases: ["--title", "-t"],
            description: "Document title to include in manifest.json.");

        var entryPointOption = new Option<string?>(
            aliases: ["--entry-point", "-e"],
            description: "Relative path to the entry-point Markdown file within the archive.");

        var languageOption = new Option<string?>(
            aliases: ["--language", "-l"],
            description: "BCP 47 language tag for the document (e.g. 'en', 'fr-CA'). Defaults to 'en'.");

        var authorOption = new Option<string?>(
            aliases: ["--author", "-a"],
            description: "Author name (optional).");

        var descriptionOption = new Option<string?>(
            aliases: ["--description", "-d"],
            description: "Short description of the document.");

        var versionOption = new Option<string?>(
            aliases: ["--doc-version"],
            description: "Version of the document itself (e.g. '1.0.0').");

        var cmd = new Command("create", "Create a .mdz archive from a source directory.")
        {
            sourceArg,
            outputArg,
            sourceOption,
            outputOption,
            forceOption,
            createIndexOption,
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
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? ctx.ParseResult.GetValueForArgument(outputArg);
            var force = ctx.ParseResult.GetValueForOption(forceOption);
            var createIndex = ctx.ParseResult.GetValueForOption(createIndexOption);
            var title = ctx.ParseResult.GetValueForOption(titleOption);
            var entryPoint = ctx.ParseResult.GetValueForOption(entryPointOption);
            var language = ctx.ParseResult.GetValueForOption(languageOption);
            var author = ctx.ParseResult.GetValueForOption(authorOption);
            var description = ctx.ParseResult.GetValueForOption(descriptionOption);
            var docVersion = ctx.ParseResult.GetValueForOption(versionOption);

            ctx.ExitCode = Handle(output, source, force, createIndex, title, entryPoint, language, author, description, docVersion);
        });

        return cmd;
    }

    private static int Handle(
        FileInfo? output,
        DirectoryInfo? source,
        bool force,
        bool createIndex,
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
            return 1;
        }

        if (!source.Exists)
        {
            Console.Error.WriteLine($"Error: Source directory '{source.FullName}' does not exist.");
            return 1;
        }

        // Ensure output path ends with .mdz
        var outputPath = output.FullName;
        if (!outputPath.EndsWith(".mdz", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Warning: Output file does not have a .mdz extension.");
        }

        // Build optional manifest
        Manifest? manifest = null;
        if (title is not null)
        {
            manifest = new Manifest
            {
                Mdz = "1.0.0",
                Title = title,
                EntryPoint = entryPoint,
                Language = language ?? "en",
                Description = description,
                Version = docVersion,
                Authors = author is not null ? [new Author { Name = author }] : null,
            };
        }
        else if (entryPoint is not null || language is not null || description is not null || docVersion is not null || author is not null)
        {
            Console.Error.WriteLine("Warning: Manifest options provided but --title is required to write a manifest.json. A manifest will not be written.");
        }

        try
        {
            if (File.Exists(outputPath))
            {
                if (!force)
                {
                    Console.Error.WriteLine($"Error: Output file '{outputPath}' already exists. Use --force to overwrite.");
                    return 1;
                }

                File.Delete(outputPath);
            }

            if (createIndex)
            {
                var files = Directory.EnumerateFiles(source.FullName, "*", SearchOption.AllDirectories)
                    .Select(localPath => (
                        ArchivePath: Path.GetRelativePath(source.FullName, localPath).Replace(Path.DirectorySeparatorChar, '/'),
                        LocalPath: localPath))
                    .ToList();

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
                    File.WriteAllText(tempIndexPath, BuildGeneratedIndex(markdownPaths), Encoding.UTF8);
                    files.Add(("index.md", tempIndexPath));

                    if (manifest is not null)
                        manifest.EntryPoint = "index.md";
                }

                try
                {
                    MdzArchive.CreateFromFiles(outputPath, files, manifest);
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tempIndexPath) && File.Exists(tempIndexPath))
                        File.Delete(tempIndexPath);
                }
            }
            else
            {
                MdzArchive.Create(outputPath, source.FullName, manifest);
            }

            Console.WriteLine($"Created '{outputPath}'");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string BuildGeneratedIndex(IReadOnlyList<string> markdownPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Index");
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

        return sb.ToString();
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
                sb.AppendLine($"{indentText}- [{child.Name}]({currentPath})");
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
}
