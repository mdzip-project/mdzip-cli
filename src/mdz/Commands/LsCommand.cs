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

        var cmd = new Command(
            "ls",
            "List the contents of a .mdz archive.\n\nExample:\n  mdz ls my-doc.mdz --long")
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
        var archivePath = ArchivePathResolver.ResolveInputArchivePath(archive.FullName);
        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"Error: Archive '{archivePath}' does not exist.");
            return 1;
        }

        try
        {
            if (longFormat)
            {
                var entries = MdzArchive.ListDetailed(archivePath);
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
                var paths = MdzArchive.List(archivePath);
                if (paths.Count == 0)
                {
                    Console.WriteLine("(archive is empty)");
                    return 0;
                }

                PrintTree(paths);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintTree(IReadOnlyList<string> paths)
    {
        var root = new TreeNode(string.Empty, isDirectory: true);
        foreach (var path in paths)
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var isDirectory = i < parts.Length - 1;
                var child = current.GetOrAddChild(part, isDirectory);
                current = child;
            }
        }

        PrintChildren(root, prefix: string.Empty);
    }

    private static void PrintChildren(TreeNode node, string prefix)
    {
        var children = node.Children
            .OrderByDescending(c => c.IsDirectory)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var isLast = i == children.Count - 1;
            var branch = isLast ? "`-- " : "|-- ";
            var suffix = child.IsDirectory ? "/" : string.Empty;
            Console.WriteLine($"{prefix}{branch}{child.Name}{suffix}");

            if (child.IsDirectory)
            {
                var nextPrefix = prefix + (isLast ? "    " : "|   ");
                PrintChildren(child, nextPrefix);
            }
        }
    }

    private sealed class TreeNode(string name, bool isDirectory)
    {
        public string Name { get; } = name;
        public bool IsDirectory { get; private set; } = isDirectory;
        public List<TreeNode> Children { get; } = [];

        public TreeNode GetOrAddChild(string name, bool isDirectory)
        {
            var existing = Children.FirstOrDefault(c =>
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.IsDirectory |= isDirectory;
                return existing;
            }

            var node = new TreeNode(name, isDirectory);
            Children.Add(node);
            return node;
        }
    }
}
