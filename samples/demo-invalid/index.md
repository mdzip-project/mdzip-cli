# Getting Started with .mdz

Welcome to this demo `.mdz` file. You're reading it right now in the mdzip.org browser viewer — a static HTML page that opened this archive entirely in your browser. Nothing was uploaded anywhere.

![Overview diagram](assets/overview.svg)

## What you're looking at

This file is a standard ZIP archive with an `.mdz` extension. Inside it:

| File | Purpose |
|------|---------|
| `manifest.json` | Metadata and entry point declaration |
| `index.md` | This page — the entry point |
| `format.md` | The format specification overview |
| `tools.md` | Tools and ecosystem |
| `faq.md` | Frequently asked questions |
| `assets/mdz-reader.js` | Browser-side reference logic for entry-point resolution and internal path handling |
| `assets/overview.svg` | Diagram image used in this demo |

`assets/mdz-reader.js` is included on purpose so the demo is not only content, but also an implementation artifact you can inspect, copy, and adapt when building your own `.mdz` tooling.

## Pages in this archive

- [The .mdz Format](format.md) — how the format works, manifest fields, path rules
- [Tools & Ecosystem](tools.md) — what you can build with .mdz
- [FAQ](faq.md) — common questions

---

> **Try it yourself**: rename this file to `.zip` and open it in any archive manager. The Markdown files are plain text — readable without any special software.
