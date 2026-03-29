# mdz command-line interface (CLI)

A cross-platform command-line interface for creating, extracting, validating, and inspecting `.mdz` files.

The `.mdz` format is a portable, self-contained document format that packages one or more Markdown content files together with their associated assets into a single ZIP archive. See the [MDZ specification](https://github.com/mdzip-project/mdzip-spec/blob/main/SPEC.md) for full details.

---

## Installation

Although the CLI is built with C#, binaries are distributed as prebuilt release assets. `.NET` is not required for normal CLI use.

### One-line install

**Windows** (PowerShell, will put `mdz.cmd` in `%LOCALAPPDATA%\Microsoft\WindowsApps`):

```powershell
irm "https://raw.githubusercontent.com/mdzip-project/mdz-cli/main/scripts/install.ps1?nocache=$(Get-Date -UFormat %s)" | iex
```

If you need to pin a specific release version during install:

```powershell
$env:MDZ_VERSION='vX.Y.Z'
irm "https://raw.githubusercontent.com/mdzip-project/mdz-cli/main/scripts/install.ps1?nocache=$(Get-Date -UFormat %s)" | iex
```

**Linux/macOS system-wide install** (recommended if you can use sudo):

```bash
curl -fsSL https://raw.githubusercontent.com/mdzip-project/mdz-cli/main/scripts/install.sh | sudo sh
```

**Linux/macOS** (no sudo required, but you might need to fix PATH):

```bash
curl -fsSL https://raw.githubusercontent.com/mdzip-project/mdz-cli/main/scripts/install.sh | sh
```

If `mdz` is not found after non-sudo install, add `~/.local/bin` to your PATH:

```bash
# current shell
export PATH="$HOME/.local/bin:$PATH"

# persist for bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc && source ~/.bashrc

# persist for zsh
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc && source ~/.zshrc
```

After install, run:

```bash
mdz --help
```

---

## Usage

```text
mdz <command> [options]
mdz create <source> <output> [options]
mdz add <archive> <entry-path> <file>
mdz remove <archive> <entry-path>
mdz extract <archive> [options]
mdz validate <archive>
mdz ls <archive> [options]
mdz inspect <archive>
```

Note: `<archive>` is the `.mdz` file path; `.mdz` extension is optional.

### Root options

| Option | Description |
|--------|-------------|
| `-v`, `--version` | Show version information |
| `-?`, `-h`, `--help` | Show help and usage information |

### Commands

| Command | Description |
|---------|-------------|
| `create` | Create an `.mdz` archive from a source directory |
| `add` | Add or replace a file entry in an existing `.mdz` archive |
| `remove` | Remove a file entry from an existing `.mdz` archive |
| `extract` | Extract the contents of an `.mdz` archive |
| `validate` | Validate an `.mdz` archive against the specification |
| `ls` | List the contents of an `.mdz` archive |
| `inspect` | Inspect metadata and manifest information of an `.mdz` archive |

---

### `mdz create <source> <output> [options]`

Creates an `.mdz` archive from all files in a source directory.
If no unambiguous entry point is found and you are in an interactive terminal, `mdz` prompts to generate a default `index.md`.

```bash
mdz create ./my-doc-folder my-doc.mdz --title "My Document" --author "Jane Smith" --entry-point index.md
mdz create --source ./my-doc-folder --output my-doc.mdz --force
mdz create ./my-doc-folder my-doc.mdz --create-index
mdz create ./my-doc-folder my-doc.mdz --map-files --entry-point index.md
mdz create ./my-doc-folder my-doc.mdz --filter "**/*.md" --filter "**/*.png" --filter "**/*.jpg" --filter "**/*.jpeg" --filter "**/*.webp" --filter "**/*.svg"
```

| Option | Description |
|--------|-------------|
| `--source, -s` | Required source directory (can also be provided positionally as `<source>`) |
| `--output, -o` | Required output archive path (can also be provided positionally as `<output>`). If no extension is supplied, `.mdz` is added automatically |
| `--filter, -fi` | Glob filter (archive-relative, repeatable). If omitted, defaults to markdown and common image files |
| `--force, -f` | Overwrite output file if it already exists |
| `--create-index, -ci` | Auto-generate `index.md` when no unambiguous entry point can be resolved |
| `--map-files, -mf` | `*` Write/update `manifest.json files[]` mapping (`path`, `originalPath`, `title`). Invalid source paths are sanitized when needed |
| `--title, -t` | `*` Sets `manifest.title` |
| `--entry-point, -e` | `*` Sets `manifest.entryPoint` |
| `--language, -l` | `*` Sets `manifest.language` (BCP 47, for example `en`, `fr-CA`). Defaults to `en` |
| `--author, -a` | `*` Sets `manifest.author.name` |
| `--description, -d` | `*` Sets `manifest.description` |
| `--doc-version` | `*` Sets `manifest.version` (for example `1.0.0`) |

Notes:
- `*` = Manifest-writing option. Passing any `*` option writes `manifest.json`.
- `mdz create` writes modern spec metadata to `manifest.spec` (`name` and `version`).
- `manifest.producer` is optional in the spec; this CLI writes it by default.
- If manifest-writing options are used without `--title`, title defaults to the source folder name.
- If invalid archive path characters are found and `--map-files` is not passed, interactive mode prompts to enable file mapping.

Example manifest generated by `mdz create`:

```json
{
  "spec": {
    "name": "mdzip-spec",
    "version": "1.0.1-draft"
  },
  "title": "My Document",
  "entryPoint": "index.md",
  "language": "en",
  "producer": {
    "application": {
      "name": "mdz-cli",
      "version": "<current mdz-cli version>",
      "url": "https://github.com/mdzip-project/mdz-cli"
    },
    "core": {
      "name": "mdz-core",
      "version": "<current mdz-core version>",
      "url": "https://github.com/mdzip-project/mdz-core"
    }
  },
  "author": {
    "name": "Jane Smith"
  }
}
```

---

### `mdz add <archive> <entry-path> <file>`

Adds a local file into an existing archive at the given archive-relative path. If the entry path already exists, it is replaced.

```bash
mdz add my-doc.mdz assets/images/cover.png ./cover.png
mdz add my-doc.mdz chapter-02.md ./chapter-02.md
```

---

### `mdz remove <archive> <entry-path>`

Removes a file entry from an existing archive.

```bash
mdz remove my-doc.mdz assets/images/old-cover.png
```

---

### `mdz extract <archive> [options]`

Extracts an `.mdz` archive to a destination directory.

```bash
mdz extract my-doc.mdz --output ./extracted
```

| Option | Description |
|--------|-------------|
| `--allow-invalid` | Extract even if archive validation fails |
| `--output, -o` | Destination directory. Defaults to a directory named after the archive in the current folder |

---

### `mdz validate <archive>`

Validates an `.mdz` archive against the specification. Exits with code `0` if valid, `1` if invalid.

```bash
mdz validate my-doc.mdz
```

---

### `mdz ls <archive> [options]`

Lists the contents of an `.mdz` archive.

```bash
mdz ls my-doc.mdz
mdz ls my-doc.mdz --long
```

| Option | Description |
|--------|-------------|
| `--long, -l` | Show detailed information (size, compressed size, last modified) |

---

### `mdz inspect <archive>`

Displays metadata and manifest information from a `.mdz` archive.

```bash
mdz inspect my-doc.mdz
```

`inspect` surfaces `manifest.spec.version` (falling back to legacy `mdz` if present), along with author and entry-point metadata.

---

## Archive structure

A `.mdz` file follows the [MDZ specification](https://github.com/mdzip-project/mdzip-spec/blob/main/SPEC.md):

```text
document.mdz
|- index.md               # Recommended entry point
|- manifest.json          # Optional metadata
|- chapter-01.md
`- assets/
   `- images/
      `- cover.png
```

## Development

Implementation note: this project is written in C# and targets .NET 8.

### Browser Builder (experimental)

A standalone browser page is available at [mdzip.org/packager.html](https://mdzip.org/packager.html). It accepts a dropped or selected folder, builds an `.mdz`, previews the resolved entry-point markdown, and provides a download link for the generated archive.

Building from source requires [.NET 8 SDK](https://dotnet.microsoft.com/download).
For Linux/macOS setup help, see [Install .NET on Linux/macOS](./INSTALL_DOTNET_LINUX_MACOS.md).

```bash
# Build
dotnet build

# Test
dotnet test

# Run directly
dotnet run --project src/mdz.Cli/mdz.Cli.csproj -- --help

# Publish a single self-contained executable (example: Windows x64)
dotnet restore .\mdz-cli.slnx --configfile .\NuGet.Config
dotnet publish src/mdz.Cli/mdz.Cli.csproj -c Release -r win-x64 --self-contained true --no-restore -p:DebugType=None -p:DebugSymbols=false -o out/win-x64

# Or use the helper script (restores + publishes + renames to mdz.exe/mdz)
powershell -ExecutionPolicy Bypass -File .\scripts\publish-single.ps1 -Rid win-x64 -ConfigFile .\NuGet.Config
```
