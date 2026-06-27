# WinGet Publishing

The CLI publishes Windows release assets as self-contained zip archives. The WinGet package identifier is:

```text
MDZipProject.mdz
```

## One-time setup

WinGet updates require an existing package manifest in `microsoft/winget-pkgs`. For the first release, publish the GitHub release assets, then run the helper locally in interactive mode:

```powershell
.\scripts\update-winget.ps1 `
  -Mode New `
  -PackageIdentifier MDZipProject.mdz `
  -Version 1.2.0 `
  -Tag v1.2.0 `
  -Token <github-pat>
```

When WingetCreate prompts for metadata, use `MDZipProject.mdz` as the package identifier. The release zip assets are architecture-qualified as `x64` and `arm64`.

## Release automation

After the first manifest is accepted, set a repository secret named `WINGET_TOKEN`. It must be a GitHub personal access token that WingetCreate can use to submit pull requests to `microsoft/winget-pkgs`.

On every `v*` tag, `.github/workflows/release.yml` builds and uploads these Windows assets:

```text
mdz-vX.Y.Z-win-x64.zip
mdz-vX.Y.Z-win-arm64.zip
```

The workflow then calls:

```powershell
.\scripts\update-winget.ps1 `
  -PackageIdentifier MDZipProject.mdz `
  -Version X.Y.Z `
  -Tag vX.Y.Z `
  -Token $env:WINGET_TOKEN `
  -Submit
```

If `WINGET_TOKEN` is not configured, the workflow skips WinGet submission while still publishing the GitHub release assets.
