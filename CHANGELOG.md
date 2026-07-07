# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed
- Published binaries are now trimmed (`PublishTrimmed` with `TrimMode=partial`), cutting the self-contained executable from ~71 MB to ~15 MB. Only the trim-annotated BCL is trimmed; `mdzip-core` and `System.CommandLine` are kept whole, and `JsonSerializerIsReflectionEnabledByDefault=true` preserves the reflection-based manifest JSON serialization (full trimming breaks it).

## [1.3.0] - 2026-06-29

### Added
- New commands: `cat` (print a packaged file), `assets` (list/inspect packaged assets), `manifest` (read/edit manifest fields), `workspace` (workspace operations), and `info` (build/version details).
- WinGet publishing support: `scripts/update-winget.ps1`, `docs/winget.md`, and a `winget` job in the release workflow (skipped unless `WINGET_TOKEN` is configured).

### Changed
- Upgraded the CLI and command library to target **.NET 10** (`net10.0`).
- Updated the `mdzip-core` dependency to `1.3.0` (multi-targeted `net8.0;net10.0`), aligning the generated manifest `spec.version` to `1.3.0`.
- Updated CLI package version to `1.3.0`.
- Expanded `inspect` and `validate` output.

### Fixed
- Install scripts (`install.ps1`/`install.sh`) now fall back to an unauthenticated GitHub request when an ambient `GITHUB_TOKEN` is expired or invalid, instead of failing the install with a `401`.

## [1.1.0] - 2026-04-13

### Added
- `mdz create --mode` option to write `manifest.mode` (`document` or `project`).
- `mdz inspect` output now includes `manifest.mode` when present.
- Help and README documentation for project mode usage.

### Changed
- Updated generated manifest `spec.version` to `1.1.0`.
- Updated CLI package version to `1.1.0`.
- Updated `mdzip-core` dependency to `1.1.0`.
- Migrated code namespaces to `MDZip` (capital Z) for consistency.
- CI and release workflows now restore from default NuGet.org sources (no GitHub Packages auth required).

### Fixed
- Release failures caused by `401 Unauthorized` when workflows attempted to restore `mdzip-core` from GitHub Packages.
