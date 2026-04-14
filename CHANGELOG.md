# Changelog

All notable changes to this project will be documented in this file.

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
