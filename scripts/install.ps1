$ErrorActionPreference = "Stop"

$RepoOwner = if ($env:REPO_OWNER) { $env:REPO_OWNER } else { "kylemwhite" }
$RepoName = if ($env:REPO_NAME) { $env:REPO_NAME } else { "mdz-cli" }
$Version = if ($env:MDZ_VERSION) { $env:MDZ_VERSION } else { $null }
$GitHubToken = if ($env:GITHUB_TOKEN) { $env:GITHUB_TOKEN } else { $null }

$InstallRoot = if ($env:INSTALL_ROOT) { $env:INSTALL_ROOT } else { Join-Path $env:LOCALAPPDATA "mdz-cli" }
$BinDir = if ($env:BIN_DIR) { $env:BIN_DIR } else { Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps" }

$arch = $null
try {
    $runtimeArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    if ($null -ne $runtimeArch) {
        $arch = $runtimeArch.ToString().ToLowerInvariant()
    }
}
catch {
    # Fall back to processor environment variables below.
}

if ([string]::IsNullOrWhiteSpace($arch)) {
    $procArch = "$($env:PROCESSOR_ARCHITEW6432) $($env:PROCESSOR_ARCHITECTURE)".ToUpperInvariant()
    if ($procArch -match "ARM64") {
        $arch = "arm64"
    }
    elseif ($procArch -match "AMD64|X64") {
        $arch = "x64"
    }
}

$rid = switch ($arch) {
    "x64" { "win-x64" }
    "arm64" { "win-arm64" }
    default { throw "Unsupported architecture '$arch'. Supported: x64, arm64." }
}

$headers = @{}
if ($GitHubToken) {
    $headers["Authorization"] = "Bearer $GitHubToken"
}
$headers["Accept"] = "application/vnd.github+json"
$headers["User-Agent"] = "mdz-cli-installer"

if (-not $Version) {
    $latestUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    try {
        $latest = Invoke-RestMethod -Uri $latestUrl -Headers $headers
    }
    catch {
        throw "Failed to query latest GitHub release from '$latestUrl'. Set MDZ_VERSION explicitly (for example: v1.0.0). Details: $($_.Exception.Message)"
    }

    if ($null -eq $latest) {
        throw "GitHub release API returned no data. Set MDZ_VERSION explicitly (for example: v1.0.0)."
    }

    $Version = $latest.tag_name
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "GitHub release API did not include a tag_name. Set MDZ_VERSION explicitly (for example: v1.0.0)."
    }
}

if (-not $Version) {
    throw "Could not determine release version. Set MDZ_VERSION (for example: v1.0.0)."
}

$assetName = "mdz-$Version-$rid.zip"
$downloadUrl = "https://github.com/$RepoOwner/$RepoName/releases/download/$Version/$assetName"

$TempRoot = Join-Path $env:TEMP ("mdz-install-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TempRoot | Out-Null

try {
    $zipPath = Join-Path $TempRoot "mdz.zip"

    Write-Host "Installing $RepoOwner/$RepoName $Version ($rid)..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -Headers $headers -ErrorAction Stop

    if (Test-Path $InstallRoot) {
        Remove-Item -Recurse -Force $InstallRoot
    }
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $InstallRoot -Force

    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
    $cmdPath = Join-Path $BinDir "mdz.cmd"
    Set-Content -Path $cmdPath -NoNewline -Value "@echo off`r`n""$InstallRoot\mdz.exe"" %*`r`n"

    Write-Host "Installed files: $InstallRoot"
    Write-Host "Launcher: $cmdPath"
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item -Recurse -Force $TempRoot
    }
}
