using System.CommandLine;
using System.CommandLine.Invocation;
using MDZip.Core;

namespace MDZip.Commands;

/// <summary>
/// Implements the 'mdz validate' subcommand.
/// Validates a .mdz archive against the specification.
/// </summary>
public static class ValidateCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to validate.");

        var cmd = new Command(
            "validate",
            "Validate a .mdz archive against the specification.\n\nExample:\n  mdz validate my-doc.mdz")
        {
            archiveArg,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            ctx.ExitCode = Handle(archive!);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive)
    {
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        var result = MdzArchive.Validate(archivePath);
        var archiveName = Path.GetFileName(archivePath);

        // A missing manifest.json isn't a conformance problem for the archive (§6, §14.2), so it's a
        // note rather than a warning and doesn't change the status.
        var noManifest = result.Warnings.Where(IsNoManifestWarning).ToList();
        var warnings = result.Warnings.Except(noManifest).ToList();
        var status = MdzArchive.GetValidationStatus(new ValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Errors,
            Warnings = warnings,
        });

        Console.WriteLine($"Status: {status}");

        if (noManifest.Count > 0)
            Console.WriteLine("  Note: No manifest.json (allowed; a conforming producer would have written one). Version metadata is unavailable.");

        foreach (var warning in warnings)
            Console.WriteLine($"  Warning: {warning}");

        if (result.IsValid)
        {
            Console.WriteLine($"'{archiveName}' is valid.");
            return 0;
        }

        foreach (var error in result.Errors)
            Console.Error.WriteLine($"  {error}");

        Console.Error.WriteLine($"'{archiveName}' is INVALID ({result.Errors.Count} error(s)).");
        return 1;
    }

    private static bool IsNoManifestWarning(string warning) =>
        warning.StartsWith("No manifest.json present", StringComparison.Ordinal);
}
