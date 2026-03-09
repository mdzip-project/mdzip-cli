# mdz-cli

A .NET command-line interface written in C# for creating, extracting, validating, and inspecting `.mdz` files.

The `.mdz` format is a portable, self-contained document format that packages one or more Markdown content files together with their associated assets into a single ZIP archive. See the [MDZ specification](https://github.com/kylemwhite/markdownzip-spec/blob/main/SPEC.md) for full details.

---

## Installation

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.

```bash
git clone https://github.com/kylemwhite/mdz-cli.git
cd mdz-cli
dotnet build src/mdz/mdz.csproj
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

### `mdz create <output> <source> [options]`

Creates a `.mdz` archive from all files in a source directory.

```bash
mdz create my-doc.mdz ./my-doc-folder --title "My Document" --author "Jane Smith" --entry-point index.md
```

| Option | Short | Description |
|--------|-------|-------------|
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

```bash
# Build
dotnet build

# Test
dotnet test

# Run directly
dotnet run --project src/mdz/mdz.csproj -- --help
```

