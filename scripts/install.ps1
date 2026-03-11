$ErrorActionPreference = "Stop"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is required. Install .NET 10 SDK first."
}

$RepoOwner = if ($env:REPO_OWNER) { $env:REPO_OWNER } else { "kylemwhite" }
$RepoName = if ($env:REPO_NAME) { $env:REPO_NAME } else { "mdz-cli" }
$RepoRef = if ($env:REPO_REF) { $env:REPO_REF } else { "main" }

$InstallRoot = if ($env:INSTALL_ROOT) { $env:INSTALL_ROOT } else { Join-Path $env:LOCALAPPDATA "mdz-cli" }
$BinDir = if ($env:BIN_DIR) { $env:BIN_DIR } else { Join-Path $env:USERPROFILE ".local\bin" }

$TempRoot = Join-Path $env:TEMP ("mdz-install-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $TempRoot | Out-Null

try {
    $zipUrl = "https://github.com/$RepoOwner/$RepoName/archive/refs/heads/$RepoRef.zip"
    $zipPath = Join-Path $TempRoot "src.zip"
    $extractPath = Join-Path $TempRoot "src"

    Write-Host "Downloading $RepoOwner/$RepoName ($RepoRef)..."
    Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath

    Write-Host "Extracting sources..."
    Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
    $srcDir = Join-Path $extractPath "$RepoName-$RepoRef"

    Write-Host "Publishing CLI..."
    if (Test-Path $InstallRoot) {
        Remove-Item -Recurse -Force $InstallRoot
    }
    New-Item -ItemType Directory -Path $InstallRoot | Out-Null
    dotnet publish (Join-Path $srcDir "src\mdz\mdz.csproj") -c Release -o $InstallRoot | Out-Null

    Write-Host "Installing launcher..."
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
    $cmdPath = Join-Path $BinDir "mdz.cmd"
    Set-Content -Path $cmdPath -NoNewline -Value "@echo off`r`ndotnet ""$InstallRoot\mdz.dll"" %*`r`n"

    Write-Host "Installed mdz to: $InstallRoot"
    Write-Host "Launcher: $cmdPath"
    Write-Host "If needed, add this folder to PATH: $BinDir"
}
finally {
    if (Test-Path $TempRoot) {
        Remove-Item -Recurse -Force $TempRoot
    }
}
