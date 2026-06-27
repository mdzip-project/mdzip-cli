[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,

    [string]$Configuration = "Release",
    [string]$OutputRoot = "out",
    [string]$ConfigFile,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot "src/mdz.Cli/mdz.Cli.csproj"
$outputDir = Join-Path $repoRoot (Join-Path $OutputRoot $Rid)

if (-not (Test-Path $projectPath)) {
    throw "Could not find CLI project at '$projectPath'."
}

if (-not $NoRestore) {
    $restoreArgs = @("restore", (Join-Path $repoRoot "mdz-cli.slnx"))
    if ($ConfigFile) {
        $resolvedConfig = if ([System.IO.Path]::IsPathRooted($ConfigFile)) { $ConfigFile } else { Join-Path $repoRoot $ConfigFile }
        $restoreArgs += @("--configfile", $resolvedConfig)
    }

    Write-Host "Restoring dependencies..."
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }
}

$publishArgs = @(
    "publish", $projectPath,
    "-c", $Configuration,
    "-r", $Rid,
    "--self-contained", "true",
    "--no-restore",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", $outputDir
)

Write-Host "Publishing single-file executable for RID '$Rid'..."
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$expectedExeName = if ($Rid -like "win-*") { "mdz.exe" } else { "mdz" }
$expectedExePath = Join-Path $outputDir $expectedExeName
if (-not (Test-Path $expectedExePath)) {
    throw "Expected published executable '$expectedExeName' was not found in '$outputDir'."
}

Write-Host "Done. Output: $outputDir"
