using System.CommandLine;

namespace Mdz.Cli;

internal static class HelpPrinter
{
    public static void PrintRootHelp(RootCommand root, string version)
    {
        Console.WriteLine("Description:");
        Console.WriteLine($"  mdz - command-line tool for creating, extracting, validating, and inspecting .mdz files. (v{version})");
        Console.WriteLine();

        Console.WriteLine("Usage:");
        Console.WriteLine("  mdz <command> [options]");
        Console.WriteLine("  mdz create <source> <output> [options]");
        Console.WriteLine("  mdz extract <archive> [options]");
        Console.WriteLine("  mdz validate <archive>");
        Console.WriteLine("  mdz ls <archive> [options]");
        Console.WriteLine("  mdz inspect <archive>");
        Console.WriteLine("  Note: <archive> is the .mdz file path; '.mdz' extension is optional.");
        Console.WriteLine();

        Console.WriteLine("Commands:");
        foreach (var command in root.Subcommands)
            Console.WriteLine($"  {command.Name,-24} {command.Description}");
        Console.WriteLine();

        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version            Show version information");
        Console.WriteLine("  -?, -h, --help           Show help and usage information");
        Console.WriteLine();

        Console.WriteLine("Command Options:");
        foreach (var command in root.Subcommands)
        {
            Console.WriteLine($"  {command.Name}:");
            if (command.Options.Count == 0)
            {
                Console.WriteLine("    (none)");
                continue;
            }

            foreach (var option in GetOrderedOptions(command))
            {
                var aliases = string.Join(", ", option.Aliases.OrderBy(a => a, StringComparer.OrdinalIgnoreCase));
                Console.WriteLine($"    {aliases,-22} {option.Description}");
            }

            if (command.Name.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("    * = Manifest-writing option. Passing any * option writes manifest.json.");
            }
        }
    }

    private static IEnumerable<Option> GetOrderedOptions(Command command)
    {
        if (!command.Name.Equals("create", StringComparison.OrdinalIgnoreCase))
            return command.Options.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase);

        var priority = new[]
        {
            "--source",
            "--output",
            "--filter",
            "--force",
            "--create-index",
            "--map-files",
            "--title",
            "--entry-point",
            "--language",
            "--author",
            "--description",
            "--doc-version",
        };

        var byAlias = command.Options
            .SelectMany(option => option.Aliases.Select(alias => (alias, option)))
            .Where(x => x.alias.StartsWith("--", StringComparison.Ordinal))
            .GroupBy(x => x.alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().option, StringComparer.OrdinalIgnoreCase);

        var ordered = new List<Option>();
        foreach (var alias in priority)
        {
            if (byAlias.TryGetValue(alias, out var option) && !ordered.Contains(option))
                ordered.Add(option);
        }

        foreach (var option in command.Options.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!ordered.Contains(option))
                ordered.Add(option);
        }

        return ordered;
    }
}
