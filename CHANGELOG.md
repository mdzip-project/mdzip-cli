# Changelog

All notable changes to this project will be documented in this file.

## [2.0.0] - 2026-10-14

Implements MDZip spec **1.2.0** on `MDZip.Core` 2.0.0 ([#7](https://github.com/mdzip-project/mdzip-cli/issues/7)).

### Changed
- **Paths are case-sensitive** after NFC normalization (§5.4.1). `cat`, `create --entry-point`, `workspace export` and `validate` suggest a case-only match ("did you mean ...?") instead of using it. `ls` and the generated index no longer merge folders that differ only in case.
- **`create` always writes `manifest.json`** with `spec.version` `1.2.0`, `title` (the source folder name unless `--title`), `entryPoint` and `mode` (§14.1). A source `manifest.json` packaged with `--filter` is kept as-is and completed, and its `entryPoint` is honoured.
- Entry point discovery in `create` follows §5.5, setting aside root `README`/`AGENTS`/`LICENSE`/`CHANGELOG`/`CONTRIBUTING` files. An invalid `--entry-point` fails with `ERR_ENTRYPOINT_INVALID`, a missing one with `ERR_ENTRYPOINT_MISSING`.
- `add`, `remove` and `manifest set` keep unknown manifest fields and untouched entries byte-for-byte, add a manifest to an archive without one, and refuse a change that would move the entry point (`ERR_ENTRYPOINT_CHANGED`).
- `validate` reports spec error codes (`ERR_ENTRYPOINT_INVALID` vs `ERR_ENTRYPOINT_MISSING`, `ERR_DUPLICATE_PATH`, `ERR_PATH_CONFLICT`, `ERR_ZIP_INVALID`), warns about symbolic link entries (which are never followed or extracted as links), and reports ignored manifest fields and duplicate members. A missing `manifest.json` is now a note and no longer makes the status `warning`.
- `inspect` explains why there's no entry point instead of printing a generic message.
- Updated `MDZip.Core` to `2.0.0`.

### Tests
- Spec fixtures re-synced to spec `v1.2.0`; new command-level tests for entry sets (T-013), unsupported compression (T-014), project mode (T-015), symbolic links (T-017, P-008) and round trips (P-004, P-005).

## [1.3.1] - 2026-07-08

### Changed
- Published binaries are now trimmed (`PublishTrimmed` with `TrimMode=partial`), cutting the self-contained executable from ~71 MB to ~15 MB. Only the trim-annotated BCL is trimmed; `mdzip-core` and `System.CommandLine` are kept whole, and `JsonSerializerIsReflectionEnabledByDefault=true` preserves the reflection-based manifest JSON serialization (full trimming breaks it).
- Updated the `mdzip-core` dependency to `1.3.3` — orphan-asset detection now recognizes raw HTML `<img src>` references.
- WinGet manifests for `MDZip.Cli` are authored in-repo under `winget/` (initial 1.3.0 submission pending in microsoft/winget-pkgs).

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
