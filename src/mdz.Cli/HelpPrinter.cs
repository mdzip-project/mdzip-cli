using System.CommandLine;

namespace Mdz.Cli;

internal static class HelpPrinter
{
    private const int MaxLineWidth = 80;

    public static void PrintRootHelp(RootCommand root, string version)
    {
        Console.WriteLine("Description:");
        WriteWrapped(
            $"  mdz - command-line tool for creating, extracting, validating, " +
            $"inspecting, and editing .mdz files. (v{version})");
        Console.WriteLine();

        Console.WriteLine("Usage:");
        Console.WriteLine("  mdz <command> [options]");
        Console.WriteLine("  mdz create <source> <output> [options]");
        Console.WriteLine("  mdz add <archive> <entry-path> <file>");
        Console.WriteLine("  mdz remove <archive> <entry-path>");
        Console.WriteLine("  mdz extract <archive> [options]");
        Console.WriteLine("  mdz validate <archive>");
        Console.WriteLine("  mdz ls <archive> [options]");
        Console.WriteLine("  mdz inspect <archive>");
        WriteWrapped("  Note: <archive> is the .mdz file path; '.mdz' extension is optional.");
        Console.WriteLine();

        Console.WriteLine("Commands:");
        foreach (var command in root.Subcommands)
        {
            var summary = GetSummaryLine(command.Description);
            WriteWrappedWithPrefix(
                $"  {command.Name,-24} ",
                summary);
        }
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
                WriteWrappedWithPrefix(
                    $"    {aliases,-22} ",
                    option.Description ?? string.Empty);
            }

            if (command.Name.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                WriteWrapped("    * = Manifest-writing option. Passing any * option writes manifest.json (spec/version metadata included).");
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
            "--no-interactive",
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

    private static void WriteWrapped(string text)
    {
        var leadingSpaces = 0;
        while (leadingSpaces < text.Length && text[leadingSpaces] == ' ')
            leadingSpaces++;

        var prefix = leadingSpaces > 0 ? new string(' ', leadingSpaces) : string.Empty;
        var body = text[leadingSpaces..];
        WriteWrappedWithPrefix(prefix, body);
    }

    private static void WriteWrappedWithPrefix(string prefix, string text)
    {
        var words = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
        {
            Console.WriteLine(prefix.TrimEnd());
            return;
        }

        var continuationPrefix = new string(' ', prefix.Length);
        var currentPrefix = prefix;
        var currentLine = currentPrefix;

        foreach (var word in words)
        {
            var separator = currentLine.Length == currentPrefix.Length ? string.Empty : " ";
            if (currentLine.Length + separator.Length + word.Length > MaxLineWidth)
            {
                Console.WriteLine(currentLine.TrimEnd());
                currentPrefix = continuationPrefix;
                currentLine = currentPrefix + word;
                continue;
            }

            currentLine += separator + word;
        }

        Console.WriteLine(currentLine.TrimEnd());
    }

    private static string GetSummaryLine(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        var trimmed = description.Trim();
        var newlineIndex = trimmed.IndexOfAny(['\r', '\n']);
        if (newlineIndex < 0)
            return trimmed;

        return trimmed[..newlineIndex].TrimEnd();
    }
}
