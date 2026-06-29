# Winget — publishing (interactive, no PAT)

We publish to `microsoft/winget-pkgs` interactively via WingetCreate's browser
sign-in. No personal access token or repo secret is needed.

- Run on **Windows** (wingetcreate is auto-downloaded by the script if missing).
- First run opens a GitHub sign-in (OAuth device-code flow) and caches the
  credential; later runs reuse it (re-prompts only when it expires).

## First publish (one time) — v1.3.0

The `v1.3.0` release assets are already published, so this is ready to run:

```powershell
cd "f:\Code\1 Projects\mdzip-project\mdzip-cli"
.\scripts\update-winget.ps1 -Mode New -PackageIdentifier MDZip.Cli -Version 1.3.0 -Tag v1.3.0
```
When wingetcreate prompts:
- **PackageIdentifier**: `MDZip.Cli`
- **Moniker**: `mdz` (enables the short `winget install mdz`)
- Fill publisher / package name / license / short description.
- Sign in to GitHub when prompted, then confirm submit → opens a PR against
  `microsoft/winget-pkgs`.

Wait for Microsoft's bots/maintainers to validate and merge the PR. Then:
```powershell
winget install MDZip.Cli
```

## Every release after that

Once the manifest exists, each new tagged release is a one-line interactive
update (replace the version/tag):

```powershell
cd "f:\Code\1 Projects\mdzip-project\mdzip-cli"
.\scripts\update-winget.ps1 -Mode Update -PackageIdentifier MDZip.Cli -Version 1.4.0 -Tag v1.4.0 -Submit
```
This pulls the `win-x64` / `win-arm64` zips from the `v1.4.0` GitHub release,
updates the manifest, and submits the PR using your cached GitHub sign-in.

## Notes
- Run this **after** the GitHub release for that tag exists (the script reads its assets).
- The `winget` job in `release.yml` only runs when a `WINGET_TOKEN` secret is set;
  since we publish interactively, that job simply skips — harmless, ignore it.
- An expired cached credential just triggers a fresh browser sign-in.
- Detail/automation reference: [docs/winget.md](docs/winget.md).
