# WinGet Publishing

The CLI's WinGet manifests are **authored and kept in this repo** under:

```text
winget/MDZip.Cli/<version>/
  MDZip.Cli.yaml                 # version manifest
  MDZip.Cli.installer.yaml       # zip installer + nested portable mdz.exe
  MDZip.Cli.locale.en-US.yaml    # defaultLocale
```

- **PackageIdentifier:** `MDZip.Cli`  **Moniker:** `mdz`
- Windows assets are self-contained zips (`mdz-vX.Y.Z-win-x64.zip` /
  `-win-arm64.zip`) each containing a single portable `mdz.exe`, aliased to
  `mdz` on install.
- We publish **interactively** — no PAT or repo secret required. The only
  interactive step is a GitHub sign-in during `wingetcreate submit`.

## Cutting a release

1. Copy the previous version folder:
   ```powershell
   cp -r winget/MDZip.Cli/1.3.0 winget/MDZip.Cli/<new>
   ```
2. In the new folder, update:
   - **all three files:** `PackageVersion: <new>`
   - **installer:** both `InstallerUrl`s (→ `v<new>`) and both `InstallerSha256`s
   - **locale:** `ReleaseNotesUrl`

   Compute the hashes (winget wants **uppercase** hex):
   ```powershell
   (Get-FileHash <asset>.zip -Algorithm SHA256).Hash      # or: curl -sL <url> | sha256sum
   ```
3. Validate locally:
   ```powershell
   winget validate --manifest winget/MDZip.Cli/<new>
   ```
4. Submit (browser sign-in, no token):
   ```powershell
   wingetcreate submit --prtitle "New version: MDZip.Cli <new>" winget/MDZip.Cli/<new>
   ```

Run after the GitHub release for that tag exists (the URLs/hashes reference its
assets). The same `wingetcreate submit <folder>` is used for the first publish
and every update. `ManifestVersion` is `1.12.0` (the current client schema);
`winget validate` will tell you if a newer client requires a bump.

## Quick checklist

See [winget-todo.md](../winget-todo.md) for the short step list.

## Alternative paths (not the default)

These exist but are **not** needed for the interactive, in-repo flow above:

- `scripts/update-winget.ps1` / `wingetcreate new` can *generate* manifests
  interactively instead of copy-editing the committed set.
- The `winget` job in `.github/workflows/release.yml` can auto-submit on each
  `v*` tag **if** a `WINGET_TOKEN` secret (a GitHub PAT with `public_repo`) is
  configured. Without the secret it safely skips, which is the default here.
