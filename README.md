# mdz command-line interface (CLI)

A .NET command-line interface written in C# for creating, extracting, validating, and inspecting `.mdz` files.

The `.mdz` format is a portable, self-contained document format that packages one or more Markdown content files together with their associated assets into a single ZIP archive. See the [MDZ specification](https://github.com/kylemwhite/markdownzip-spec/blob/main/SPEC.md) for full details.

---

## Installation

Binaries are distributed as prebuilt release assets. `.NET` is not required for normal CLI use.

### One-line install

Windows (PowerShell, will put `mdz.cmd` in `%LOCALAPPDATA%\Microsoft\WindowsApps` ):

```powershell
irm https://raw.githubusercontent.com/kylemwhite/mdz-cli/main/scripts/install.ps1 | iex
```

Linux/macOS system-wide install (recommended if you can use sudo):

```bash
curl -fsSL https://raw.githubusercontent.com/kylemwhite/mdz-cli/main/scripts/install.sh | sudo sh
```

Linux/macOS:

```bash
curl -fsSL https://raw.githubusercontent.com/kylemwhite/mdz-cli/main/scripts/install.sh | sh
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

```
mdz [command] [options]
```

### Commands

| Command | Description |
|---------|-------------|
| `mdz create` | Create a `.mdz` archive from a source directory |
| `mdz extract` | Extract the contents of a `.mdz` archive |
| `mdz validate` | Validate a `.mdz` archive against the specification |
| `mdz ls` | List the contents of a `.mdz` archive |
| `mdz inspect` | Inspect metadata and manifest information |

---

### `mdz create <source> <output> [options]`

Creates a `.mdz` archive from all files in a source directory.

```bash
mdz create ./my-doc-folder my-doc.mdz --title "My Document" --author "Jane Smith" --entry-point index.md
mdz create --source ./my-doc-folder --output my-doc.mdz --force
mdz create ./my-doc-folder my-doc.mdz --create-index
```

| Option | Short | Description |
|--------|-------|-------------|
| `--source` | `-s` | Source directory (alternative to positional `<source>`) |
| `--output` | `-o` | Output archive path (alternative to positional `<output>`) |
| `--force` | `-f` | Overwrite output file if it already exists |
| `--create-index` | | Auto-generate `index.md` when no unambiguous entry point can be resolved |
| `--title` | `-t` | Document title (writes `manifest.json`) |
| `--entry-point` | `-e` | Relative path to the primary Markdown file |
| `--language` | `-l` | BCP 47 language tag (e.g. `en`, `fr-CA`) |
| `--author` | `-a` | Author name |
| `--description` | `-d` | Short description of the document |
| `--doc-version` | | Document version (e.g. `1.0.0`) |

---

### `mdz extract <archive> [options]`

Extracts a `.mdz` archive to a destination directory.

```bash
mdz extract my-doc.mdz --output ./extracted
```

| Option | Short | Description |
|--------|-------|-------------|
| `--output` | `-o` | Destination directory (defaults to archive name without extension) |
| `--allow-invalid` | | Extract even if archive validation fails |

---

### `mdz validate <archive>`

Validates a `.mdz` archive against the specification. Exits with code `0` if valid, `1` if invalid.

```bash
mdz validate my-doc.mdz
```

---

### `mdz ls <archive> [options]`

Lists the contents of a `.mdz` archive.

```bash
mdz ls my-doc.mdz
mdz ls my-doc.mdz --long
```

| Option | Short | Description |
|--------|-------|-------------|
| `--long` | `-l` | Show detailed information (size, compressed size, last modified) |

---

### `mdz inspect <archive>`

Displays metadata and manifest information from a `.mdz` archive.

```bash
mdz inspect my-doc.mdz
```

---

## Archive Structure

A `.mdz` file follows the [MDZ specification](https://github.com/kylemwhite/markdownzip-spec/blob/main/SPEC.md):

```
document.mdz
├── index.md               # Recommended entry point
├── manifest.json          # Optional metadata
├── chapter-01.md
└── assets/
    └── images/
        └── cover.png
```

## Development

Building from source requires [.NET 10 SDK](https://dotnet.microsoft.com/download).
For Linux/macOS setup help, see [Install .NET on Linux/macOS](./INSTALL_DOTNET_LINUX_MACOS.md).

```bash
# Build
dotnet build

# Test
dotnet test

# Run directly
dotnet run --project src/mdz/mdz.csproj -- --help
```

