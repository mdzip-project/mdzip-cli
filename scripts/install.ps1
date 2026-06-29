$ErrorActionPreference = "Stop"

$RepoOwner = if ($env:REPO_OWNER) { $env:REPO_OWNER } else { "mdzip-project" }
$RepoName = if ($env:REPO_NAME) { $env:REPO_NAME } else { "mdzip-cli" }
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

$headers = @{
    "Accept"     = "application/vnd.github+json"
    "User-Agent" = "mdz-cli-installer"
}
if ($GitHubToken) {
    $headers["Authorization"] = "Bearer $GitHubToken"
}

# Invoke a GitHub request, falling back to an unauthenticated request if an
# ambient GITHUB_TOKEN is present but rejected (expired/invalid). mdzip-cli is a
# public repo, so a token is never required to install.
function Invoke-GitHub {
    param(
        [Parameter(Mandatory)] [string] $Uri,
        [string] $OutFile
    )
    $attempt = $headers
    for ($i = 0; $i -lt 2; $i++) {
        try {
            if ($OutFile) {
                Invoke-WebRequest -Uri $Uri -OutFile $OutFile -Headers $attempt -ErrorAction Stop | Out-Null
                return
            }
            return Invoke-RestMethod -Uri $Uri -Headers $attempt -ErrorAction Stop
        }
        catch {
            $status = $null
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }
            $canRetry = ($i -eq 0) -and $attempt.ContainsKey("Authorization") -and ($status -eq 401 -or $status -eq 403)
            if (-not $canRetry) { throw }
            Write-Warning "GitHub rejected the request (HTTP $status) using GITHUB_TOKEN; retrying without authentication. Your GITHUB_TOKEN may be expired or invalid."
            $attempt = @{}
            foreach ($k in $headers.Keys) { if ($k -ne "Authorization") { $attempt[$k] = $headers[$k] } }
        }
    }
}

if (-not $Version) {
    $latestUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    try {
        $latest = Invoke-GitHub -Uri $latestUrl
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
    Invoke-GitHub -Uri $downloadUrl -OutFile $zipPath

    if (Test-Path $InstallRoot) {
        Remove-Item -Recurse -Force $InstallRoot
    }
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
    Expand-Archive -Path $zipPath -DestinationPath $InstallRoot -Force
    $installedExe = Join-Path $InstallRoot "mdz.exe"
    if (-not (Test-Path $installedExe)) {
        throw "Release asset '$assetName' did not contain expected executable 'mdz.exe'."
    }

    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
    $cmdPath = Join-Path $BinDir "mdz.cmd"
    Set-Content -Path $cmdPath -NoNewline -Value "@echo off`r`n""$installedExe"" %*`r`n"

    Write-Host "Installed files: $InstallRoot"
    Write-Host "Launcher: $cmdPath"
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item -Recurse -Force $TempRoot
    }
}
