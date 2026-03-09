using System.CommandLine;
using System.CommandLine.Invocation;
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
        var outputArg = new Argument<FileInfo>(
            name: "output",
            description: "Path to the output .mdz file to create.");

        var sourceArg = new Argument<DirectoryInfo>(
            name: "source",
            description: "Source directory containing the files to package.");

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
            outputArg,
            sourceArg,
            titleOption,
            entryPointOption,
            languageOption,
            authorOption,
            descriptionOption,
            versionOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var output = ctx.ParseResult.GetValueForArgument(outputArg);
            var source = ctx.ParseResult.GetValueForArgument(sourceArg);
            var title = ctx.ParseResult.GetValueForOption(titleOption);
            var entryPoint = ctx.ParseResult.GetValueForOption(entryPointOption);
            var language = ctx.ParseResult.GetValueForOption(languageOption);
            var author = ctx.ParseResult.GetValueForOption(authorOption);
            var description = ctx.ParseResult.GetValueForOption(descriptionOption);
            var docVersion = ctx.ParseResult.GetValueForOption(versionOption);

            ctx.ExitCode = Handle(ctx, output!, source!, title, entryPoint, language, author, description, docVersion);
        });

        return cmd;
    }

    private static int Handle(
        InvocationContext ctx,
        FileInfo output,
        DirectoryInfo source,
        string? title,
        string? entryPoint,
        string? language,
        string? author,
        string? description,
        string? docVersion)
    {
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
            MdzArchive.Create(outputPath, source.FullName, manifest);
            Console.WriteLine($"Created '{outputPath}'");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
