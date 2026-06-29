[CmdletBinding()]
param(
    [ValidateSet("Update", "New")]
    [string] $Mode = "Update",

    [string] $PackageIdentifier = "MDZip.Cli",

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $Tag,

    [string] $RepoOwner = "mdzip-project",

    [string] $RepoName = "mdzip-cli",

    [string] $WingetCreatePath,

    [string] $Token,

    [switch] $Submit,

    [switch] $NoOpen
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-WingetCreate {
    param([string] $RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -LiteralPath $RequestedPath)) {
            throw "WingetCreatePath '$RequestedPath' does not exist."
        }

        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    $existing = Get-Command wingetcreate.exe -ErrorAction SilentlyContinue
    if ($existing) {
        return $existing.Source
    }

    $toolDir = Join-Path ([System.IO.Path]::GetTempPath()) ("wingetcreate-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $toolDir | Out-Null

    $downloadPath = Join-Path $toolDir "wingetcreate.exe"
    Invoke-WebRequest -Uri "https://aka.ms/wingetcreate/latest" -OutFile $downloadPath
    return $downloadPath
}

$wingetCreate = Get-WingetCreate -RequestedPath $WingetCreatePath
$releaseUrl = "https://github.com/$RepoOwner/$RepoName/releases/tag/$Tag"
$assetBaseUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$Tag"
$windowsAssetUrlsWithoutOverrides = @(
    "$assetBaseUrl/mdz-$Tag-win-x64.zip",
    "$assetBaseUrl/mdz-$Tag-win-arm64.zip"
)
$windowsAssetUrls = @(
    "$assetBaseUrl/mdz-$Tag-win-x64.zip|x64",
    "$assetBaseUrl/mdz-$Tag-win-arm64.zip|arm64"
)

# A token is optional: when omitted, WingetCreate uses its stored credential or
# starts an interactive GitHub sign-in. Pass -Token only for non-interactive/CI use.

if ($Mode -eq "New") {
    Write-Host "Starting interactive WinGet manifest creation for $PackageIdentifier $Version."
    Write-Host "Set PackageIdentifier to '$PackageIdentifier' when WingetCreate prompts for it."
}

if ($Mode -eq "Update") {
    $arguments = @(
        "update",
        $PackageIdentifier,
        "--version",
        $Version,
        "--urls"
    )
    $arguments += $windowsAssetUrls
    $arguments += @("--release-notes-url", $releaseUrl)
}
else {
    $arguments = @("new")
    $arguments += $windowsAssetUrlsWithoutOverrides
}

if ($Mode -eq "Update" -and $Submit) {
    $arguments += "--submit"
}
if (-not [string]::IsNullOrWhiteSpace($Token)) {
    $arguments += @("--token", $Token)
}

if ($Mode -eq "New" -and $NoOpen) {
    $arguments += "--no-open"
}

Write-Host "Running: $wingetCreate $($arguments -join ' ')"
& $wingetCreate @arguments
if ($LASTEXITCODE -ne 0) {
    throw "wingetcreate exited with code $LASTEXITCODE."
}
