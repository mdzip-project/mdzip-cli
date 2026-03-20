# The .mdz Format

`.mdz` is a ZIP archive containing Markdown documents, assets, and an optional `manifest.json` metadata file. The extension signals intent: this archive is a Markdown document bundle, not a generic ZIP.

## This archive's structure

The demo you're reading right now is an `.mdz` file. Here are its actual contents:

- [index.md](index.md) - entry point, overview page
- [format.md](format.md) - this page
- [faq.md](faq.md) - frequently asked questions
- [tools.md](tools.md) - tools and ecosystem
- [assets/overview.svg](assets/overview.svg) - archive diagram (an SVG image)
- `manifest.json` - metadata

A real-world archive might look like this:

```text
my-document.mdz
|- manifest.json        <- optional, but recommended
|- index.md             <- entry point
|- chapter-2.md
|- appendix.md
`- assets/
   |- overview.svg
   |- diagram.png
   `- screenshot.jpg
```

## Entry point discovery

When a viewer opens an `.mdz`, it finds the primary document using this algorithm:

| Priority | Condition | Result |
|----------|-----------|--------|
| 1 | `manifest.json` present with `entryPoint` field | Use that file |
| 2 | `index.md` exists at the archive root | Use it |
| 3 | Exactly one `.md` or `.markdown` file at the root | Use it |
| 4 | None of the above | Error: ambiguous entry point |

## The manifest

`manifest.json` is optional but enables richer tooling. All fields are optional, but `spec.version` and `title` are recommended.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `spec` | object | - | Spec metadata object (`name`, `version`) |
| `title` | string | - | Human-readable document title |
| `description` | string | - | Short summary |
| `author` | object | - | Primary author (`name`, `email`, `url`) |
| `producer` | object | - | Producing tool metadata (`application`, `core`) |
| `keywords` | string[] | - | Tags for indexing |
| `license` | string | - | SPDX license identifier or URL |
| `entryPoint` | string | - | Path to the primary Markdown file |
| `created` | string or object | - | ISO 8601 timestamp or object with `when` |
| `modified` | string or object | - | ISO 8601 timestamp or object with `when` |
| `cover` | string | - | Path to a cover image within the archive |
| `files` | object[] | - | Optional source-file mapping metadata |

## Path rules

All file paths inside an `.mdz` must:

- Use forward slashes as separators (`assets/photo.png`, not `assets\photo.png`)
- Not start with `/` or contain `..` components that escape the archive root
- Be encoded in UTF-8
- Be unique when compared case-insensitively on case-insensitive filesystems

## Error codes

Implementations should use these standardized error identifiers:

| Code | Meaning |
|------|---------|
| `ERR_ZIP_INVALID` | File is not a valid ZIP archive |
| `ERR_ZIP_ENCRYPTED` | Archive contains encrypted entries |
| `ERR_PATH_INVALID` | An entry path violates path rules |
| `ERR_MANIFEST_INVALID` | `manifest.json` is malformed |
| `ERR_ENTRYPOINT_UNRESOLVED` | No unambiguous entry point found |
| `ERR_ENTRYPOINT_MISSING` | `entryPoint` references a non-existent file |
| `ERR_VERSION_UNSUPPORTED` | `manifest.spec.version` major version not supported |
