# WinGet Publishing

The CLI is published to `microsoft/winget-pkgs` as:

```text
MDZip.Cli   (moniker: mdz)
```

Windows release assets are self-contained zip archives, architecture-qualified
as `x64` and `arm64`:

```text
mdz-vX.Y.Z-win-x64.zip
mdz-vX.Y.Z-win-arm64.zip
```

We publish **interactively** via WingetCreate's GitHub sign-in — no personal
access token or repo secret is required. See [winget-todo.md](../winget-todo.md)
for the step-by-step checklist.

## First publish (one time)

Run after the GitHub release for the tag exists (the helper reads its assets):

```powershell
.\scripts\update-winget.ps1 -Mode New -PackageIdentifier MDZip.Cli -Version 1.3.0 -Tag v1.3.0
```

Omitting `-Token` triggers WingetCreate's interactive browser sign-in. When
prompted, use identifier `MDZip.Cli` and moniker `mdz`, then confirm submit to
open a PR against `microsoft/winget-pkgs`.

## Subsequent releases

Once the manifest exists, each tagged release is a one-line interactive update:

```powershell
.\scripts\update-winget.ps1 -Mode Update -PackageIdentifier MDZip.Cli -Version X.Y.Z -Tag vX.Y.Z -Submit
```

This reuses your cached GitHub sign-in (re-prompts only when it expires).

## Optional: CI automation

If you ever prefer hands-off releases, set a repository secret `WINGET_TOKEN`
(a GitHub PAT with `public_repo`). The `winget` job in
`.github/workflows/release.yml` then submits the update on every `v*` tag,
passing `-Token $env:WINGET_TOKEN -Submit`. Without the secret, that job simply
skips — which is the default for the interactive workflow above.
