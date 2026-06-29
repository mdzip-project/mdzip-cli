# Winget — publish MDZip.Cli (pre-authored, interactive, no PAT)

Manifests are committed in this repo at `winget/MDZip.Cli/<version>/`.
Identifier `MDZip.Cli`, moniker `mdz`. Full reference: [docs/winget.md](docs/winget.md).
Publishing is interactive (browser sign-in) — no PAT or repo secret.

## First publish (v1.3.0 — manifests ready)

1. Validate:
   ```powershell
   winget validate --manifest winget/MDZip.Cli/1.3.0
   ```
2. Submit (signs in to GitHub in your browser, opens the PR):
   ```powershell
   wingetcreate submit --prtitle "New package: MDZip.Cli version 1.3.0" winget/MDZip.Cli/1.3.0
   ```
3. Wait for `microsoft/winget-pkgs` CI + maintainer merge. Then:
   ```powershell
   winget install MDZip.Cli      # or: winget install mdz
   ```

## Every later release

1. Copy the folder and bump:
   ```powershell
   cp -r winget/MDZip.Cli/<prev> winget/MDZip.Cli/<new>
   ```
2. In the new folder edit: `PackageVersion` (all 3 files), both `InstallerUrl`
   (`v<new>`) + `InstallerSha256` (installer), `ReleaseNotesUrl` (locale).
   ```powershell
   (Get-FileHash <asset>.zip -Algorithm SHA256).Hash   # uppercase hex winget expects
   ```
3. `winget validate --manifest winget/MDZip.Cli/<new>`
4. `wingetcreate submit --prtitle "New version: MDZip.Cli <new>" winget/MDZip.Cli/<new>`

## Notes
- Run **after** the GitHub release for that tag exists (URLs/hashes point at its assets).
- `ManifestVersion` is `1.12.0`; `winget validate` flags it if a newer client needs a bump.
- An expired cached GitHub credential just triggers a fresh browser sign-in on submit.
