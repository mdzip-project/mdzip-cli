# Frequently Asked Questions

## Is this just a ZIP file?

Yes.An `.mdz` file is a standard ZIP archive. You can rename it to `.zip` and open it in any archive manager — no special software required. The `.mdz` extension signals the intent: this is a Markdown document bundle, not a generic archive.

## Why not just use EPUB?

EPUB is ZIP-based too, but its content format is XHTML — not Markdown. Reading an EPUB requires a compatible reader. The XML internals are complex and tools are locked to specific apps.

.mdz is best thought of as an EPUB-like container for Markdown: same single-file bundling idea, but Markdown-native and much lighter in structure.

A `.mdz` contains plain Markdown. Any text editor can read it. Any programmer can parse it. Any tool that reads ZIP files can list its contents.

| | EPUB | `.mdz` |
|-|------|--------|
| Content format | XHTML (XML) | Plain Markdown |
| Readable in a text editor | ❌ (XML noise) | ✅ |
| Accessible without special reader (basic access) | ❌ | ✅ |
| Diff-friendly | ❌ | ✅ |
| Native Markdown tooling | ❌ | ✅ |

> **Why diff-friendly?** `.mdz` content is typically plain Markdown text, so version-control diffs stay readable. EPUB content is XHTML/XML, which tends to produce noisier structural diffs.

## Why not just send a ZIP with Markdown files inside?

You can! A bare `.zip` works fine for extraction. But it has no conventions: which file is the entry point? What's the title? What version of the spec was used?

`.mdz` adds a thin layer of agreed-upon structure — entry point discovery, optional metadata — without requiring any proprietary format.

## Does it work offline?

Completely. The `.mdz` file contains everything needed to read the document. There's no CDN, no hosting dependency, no expiring share link.

## Can I put non-Markdown files inside?

Yes. The spec doesn't restrict file types. Images, PDFs, CSVs, source code — any asset can be bundled. Tools are free to render what they understand and ignore what they don't.

## Can an .mdz include the stylesheet used to render Markdown?

Yes. You can include stylesheets as assets (for example, `assets/style.css`). A viewer may choose to apply them, but the format does not require every reader to support custom CSS.

## Can I put any file in an .mdz? Even code?

Yes. The format allows arbitrary asset types, including source code files. For interoperability and package size, producers should mostly include files that are actually referenced by the document.

## Can an .mdz include malicious code?

It can contain malicious files as data, just like any ZIP container. Conforming readers should treat `.mdz` content as untrusted input and must not execute scripts or binaries as part of normal rendering.

## Can an .mdz include HTML files?

Yes. HTML files can be included as assets like any other file type.

## Will HTML files be rendered automatically?

Not necessarily. Rendering behavior depends on the consumer. A basic `.mdz` reader may show HTML files as downloadable text assets, while more advanced tools may choose to preview them.

## Could I package an entire website in an .mdz file?

Yes, you can package a complete static site (HTML, CSS, JS, images) in one `.mdz` archive. Whether a reader renders it as a website is implementation-specific; the format itself allows bundling those files.

## Does CommonMark support HTML inside Markdown files?

Yes. CommonMark allows raw HTML blocks and inline HTML inside Markdown documents. In practice, rendering still depends on the consumer: some tools pass HTML through as-is, while others sanitize or restrict it for safety.

## What Markdown flavour does it use?

The spec doesn't mandate a specific Markdown variant. Authors should stick to CommonMark for maximum compatibility. GitHub Flavored Markdown (GFM) is a widely-supported superset and works well in practice.

## Is the spec stable?

The current draft version is `1.0.1-draft`. The spec version is carried in `manifest.json` at `spec.version`, so tools can detect and adapt to future changes.

Read the full specification at [github.com/kylemwhite/markdownzip-spec](https://github.com/kylemwhite/markdownzip-spec).


