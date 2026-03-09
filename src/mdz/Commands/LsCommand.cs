using System.CommandLine;
using System.CommandLine.Invocation;
using Mdz.Core;

namespace Mdz.Commands;

/// <summary>
/// Implements the 'mdz ls' subcommand.
/// Lists the contents of a .mdz archive.
/// </summary>
public static class LsCommand
{
    public static Command Build()
    {
        var archiveArg = new Argument<FileInfo>(
            name: "archive",
            description: "Path to the .mdz file to list.");

        var longOption = new Option<bool>(
            aliases: ["--long", "-l"],
            description: "Show detailed information (size, compressed size, last modified).");

        var cmd = new Command("ls", "List the contents of a .mdz archive.")
        {
            archiveArg,
            longOption,
        };

        cmd.SetHandler((InvocationContext ctx) =>
        {
            var archive = ctx.ParseResult.GetValueForArgument(archiveArg);
            var longFormat = ctx.ParseResult.GetValueForOption(longOption);
            ctx.ExitCode = Handle(archive!, longFormat);
        });

        return cmd;
    }

    private static int Handle(FileInfo archive, bool longFormat)
    {
        if (!archive.Exists)
        {
            Console.Error.WriteLine($"Error: Archive '{archive.FullName}' does not exist.");
            return 1;
        }

        try
        {
            if (longFormat)
            {
                var entries = MdzArchive.ListDetailed(archive.FullName);
                if (entries.Count == 0)
                {
                    Console.WriteLine("(archive is empty)");
                    return 0;
                }

                // Column headers
                Console.WriteLine($"{"Size",10}  {"Compressed",10}  {"Last Modified",-22}  Path");
                Console.WriteLine(new string('-', 80));

                foreach (var entry in entries)
                {
                    Console.WriteLine(
                        $"{entry.Size,10}  {entry.CompressedSize,10}  {entry.LastModified:yyyy-MM-dd HH:mm:ss}  {entry.Path}");
                }

                Console.WriteLine();
                Console.WriteLine($"{entries.Count} file(s)");
            }
            else
            {
                var paths = MdzArchive.List(archive.FullName);
                if (paths.Count == 0)
                {
                    Console.WriteLine("(archive is empty)");
                    return 0;
                }

                foreach (var path in paths)
                    Console.WriteLine(path);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
