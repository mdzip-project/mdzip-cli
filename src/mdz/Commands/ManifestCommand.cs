using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using MDZip.Core;
using MDZip.Core.Models;

namespace MDZip.Commands;

public static class ManifestCommand
{
    public static Command Build()
    {
        var cmd = new Command("manifest", "Read or update manifest metadata.")
        {
            BuildGet(),
            BuildSet(),
        };
        return cmd;
    }

    private static Command BuildGet()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var splitOption = new Option<bool>("--split", "Split editable and reserved metadata.");
        var cmd = new Command("get", "Print manifest metadata.") { archiveArg, splitOption };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = HandleGet(
                ctx.ParseResult.GetValueForArgument(archiveArg)!,
                ctx.ParseResult.GetValueForOption(splitOption));
        });
        return cmd;
    }

    private static Command BuildSet()
    {
        var archiveArg = new Argument<FileInfo>("archive", "Path to the .mdz file.");
        var titleOption = new Option<string?>("--title", "Set manifest.title.");
        var languageOption = new Option<string?>("--language", "Set manifest.language.");
        var descriptionOption = new Option<string?>("--description", "Set manifest.description.");
        var licenseOption = new Option<string?>("--license", "Set manifest.license.");
        var versionOption = new Option<string?>("--doc-version", "Set manifest.version.");
        var coverOption = new Option<string?>("--cover", "Set manifest.cover.");
        var modeOption = new Option<string?>("--mode", "Set manifest.mode.");
        var entryPointOption = new Option<string?>("--entry-point", "Set manifest.entryPoint.");
        var authorOption = new Option<string?>("--author", "Set manifest.author.name.");
        var cmd = new Command("set", "Update editable manifest metadata.")
        {
            archiveArg,
            titleOption,
            languageOption,
            descriptionOption,
            licenseOption,
            versionOption,
            coverOption,
            modeOption,
            entryPointOption,
            authorOption,
        };
        cmd.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = HandleSet(
                ctx.ParseResult.GetValueForArgument(archiveArg)!,
                ctx.ParseResult.GetValueForOption(titleOption),
                ctx.ParseResult.GetValueForOption(languageOption),
                ctx.ParseResult.GetValueForOption(descriptionOption),
                ctx.ParseResult.GetValueForOption(licenseOption),
                ctx.ParseResult.GetValueForOption(versionOption),
                ctx.ParseResult.GetValueForOption(coverOption),
                ctx.ParseResult.GetValueForOption(modeOption),
                ctx.ParseResult.GetValueForOption(entryPointOption),
                ctx.ParseResult.GetValueForOption(authorOption));
        });
        return cmd;
    }

    private static int HandleGet(FileInfo archive, bool split)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        var manifest = MdzArchive.ReadManifest(archivePath);
        if (manifest is null)
        {
            Console.WriteLine("(no manifest.json)");
            return 0;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        object value = split ? MdzArchive.SplitManifestMetadata(manifest) : manifest;
        Console.WriteLine(JsonSerializer.Serialize(value, value.GetType(), options));
        return 0;
    }

    private static int HandleSet(
        FileInfo archive,
        string? title,
        string? language,
        string? description,
        string? license,
        string? docVersion,
        string? cover,
        string? mode,
        string? entryPoint,
        string? author)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            var metadata = new ManifestEditableMetadata
            {
                Title = title,
                Language = language,
                Description = description,
                License = license,
                Version = docVersion,
                Cover = cover,
                Mode = mode,
                EntryPoint = entryPoint,
                Author = string.IsNullOrWhiteSpace(author) ? null : new ManifestAuthor { Name = author },
            };
            var manifest = MdzArchive.UpdateManifest(archivePath, metadata);
            Console.WriteLine($"Updated manifest.json ({manifest.Title ?? "untitled"}).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
