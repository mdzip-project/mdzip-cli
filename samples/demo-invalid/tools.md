# Tools & Ecosystem

`.mdz` is an open format. Any tool that can read ZIP files can read `.mdz`. Here's how different tools relate to the format.

## How .mdz fits in the Markdown ecosystem

```mermaid
graph TD
    A[Author writes Markdown] --> B[.mdz archive]
    B --> C[Browser viewer]
    B --> D[VS Code extension]
    B --> E[Obsidian plugin]
    B --> F[Native file preview]
    B --> G[Email / Slack attachment]
    B --> H[Git repository]
```

## The viewer stack

This browser viewer is built from three libraries:

| Library | Role |
|---------|------|
| [JSZip](https://stuk.github.io/jszip/) | Reads the ZIP archive in the browser |
| [marked](https://marked.js.org/) | Parses Markdown into HTML |
| [Mermaid](https://mermaid.js.org/) | Renders diagrams from code blocks |

Everything runs locally. No server, no upload, no network request for your document content.

## What the ecosystem could build

These tools don't exist yet — they're the natural next step:

```mermaid
graph LR
    SRC[Markdown source] -->|mdz CLI| MDZ[.mdz file]
    SRC -->|GitHub Action| MDZ
    MDZ -->|opened by| VS[VS Code extension]
    MDZ -->|imported into| OB[Obsidian plugin]
    MDZ -->|rendered by| BV[Browser viewer ✅]
    MDZ -->|previewed by| NP[Native previewer]
```

## Creating an .mdz today

Until the CLI exists, you can create an `.mdz` with any ZIP tool:

```bash
# Using zip on macOS/Linux
zip -r my-document.mdz index.md assets/ manifest.json

# Using PowerShell on Windows
Compress-Archive -Path index.md, assets, manifest.json -DestinationPath my-document.zip
Rename-Item my-document.zip my-document.mdz
```

See the [Use Today](https://mdzip.org/today.html) page for more options.
