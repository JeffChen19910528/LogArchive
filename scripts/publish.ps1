#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes logbackup as a self-contained, single-file executable for Windows and Linux.

.DESCRIPTION
    Cross-compiles from this Windows machine using the .NET SDK's built-in cross-publish
    support (no Linux machine or Docker required). Each target gets its own self-contained
    runtime bundled into a single executable file - just copy it to the target machine and run.

.PARAMETER RuntimeIdentifiers
    One or more .NET RIDs to publish. Defaults to win-x64 and linux-x64.

.PARAMETER OutputRoot
    Root directory for published output; each RID gets its own subfolder. Defaults to ./publish.

.EXAMPLE
    ./scripts/publish.ps1
    Publishes win-x64 and linux-x64 into ./publish/win-x64 and ./publish/linux-x64.

.EXAMPLE
    ./scripts/publish.ps1 -RuntimeIdentifiers linux-arm64
    Publishes only linux-arm64.
#>
param(
    [string[]] $RuntimeIdentifiers = @("win-x64", "linux-x64"),
    [string] $OutputRoot = (Join-Path $PSScriptRoot "..\publish")
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\LogBackup.CLI\LogBackup.CLI.csproj"

foreach ($rid in $RuntimeIdentifiers) {
    $outDir = Join-Path $OutputRoot $rid
    Write-Host "==> Publishing $rid to $outDir" -ForegroundColor Cyan

    dotnet publish $project `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $outDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for RID '$rid' (exit code $LASTEXITCODE)"
    }

    $exeName = if ($rid.StartsWith("win-")) { "logbackup.exe" } else { "logbackup" }
    $exePath = Join-Path $outDir $exeName
    if (Test-Path $exePath) {
        $sizeMb = [Math]::Round((Get-Item $exePath).Length / 1MB, 1)
        Write-Host "    -> $exePath ($sizeMb MB)" -ForegroundColor Green
    }
    else {
        throw "Expected output executable not found: $exePath"
    }
}

Write-Host "`nDone. Published executables are under $OutputRoot" -ForegroundColor Cyan
