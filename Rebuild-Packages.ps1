<#
.SYNOPSIS
    Rebuilds all YouVersion Platform SDK NuGet packages into the local feed.

.DESCRIPTION
    Packs every SDK project in topological dependency order, outputs .nupkg files
    to the local-feed folder declared in NuGet.config, clears the global NuGet
    cache for these packages, then restores PlatformTestApp so it picks up the
    fresh packages.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER SolutionRoot
    Path to the folder containing the .csproj projects and NuGet.config.
    Defaults to the folder this script lives in.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$SolutionRoot  = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$LocalFeed = Join-Path $SolutionRoot "packages"

# Projects in topological (dependency) order — leaves first.
# UsfmReferences and API.Models have no local deps, so they go first.
$projects = [ordered]@{
    "YouVersion.UsfmReferences"        = "YouVersion.UsfmReferences\YouVersion.UsfmReferences.csproj"
    "YouVersion.Platform.API.Models"   = "Platform.API.Models\Platform.API.Models.csproj"
    "YouVersion.Platform.API"          = "Platform.API\Platform.API.csproj"
    "YouVersion.Platform.SDK.Services" = "Platform.SDK.Services\Platform.SDK.Services.csproj"
    "YouVersion.Platform.SDK.Components" = "Platform.SDK.Components\Platform.SDK.Components.csproj"
}

function Write-Step([string]$msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

function Assert-LastExitCode([string]$label) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $label (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# ── 1. Prepare the local feed ────────────────────────────────────────────────
Write-Step "Preparing local NuGet feed: $LocalFeed"
New-Item -ItemType Directory -Force -Path $LocalFeed | Out-Null
Get-ChildItem -Path $LocalFeed -Include "*.nupkg","*.snupkg" -Recurse -ErrorAction SilentlyContinue |
    Remove-Item -Force
Write-Host "    Stale packages removed."

# ── 2. Clear global NuGet cache for our packages ─────────────────────────────
Write-Step "Clearing global NuGet cache for SDK packages"
$globalCacheDir = ((dotnet nuget locals global-packages --list) |
    Where-Object { $_ -match "global-packages:" }) -replace ".*global-packages:\s*", "" -replace "\s+$", ""
foreach ($id in $projects.Keys) {
    $pkgDir = Join-Path $globalCacheDir $id.ToLower()
    if (Test-Path $pkgDir) {
        Write-Host "    Removing: $pkgDir"
        Remove-Item -Recurse -Force $pkgDir
    }
}

# ── 3. Pack each project in dependency order ─────────────────────────────────
foreach ($entry in $projects.GetEnumerator()) {
    $label  = $entry.Key
    $rel    = $entry.Value
    $csproj = Join-Path $SolutionRoot $rel

    Write-Step "Packing $label"
    dotnet pack $csproj `
        --configuration $Configuration `
        --output $LocalFeed
    Assert-LastExitCode "dotnet pack $label"
}

# ── 4. Restore and build the solution ────────────────────────────────────────
# Restore first (--force) so PlatformTestApp, which consumes the SDK via the
# YouVersionLocal feed rather than a ProjectReference, picks up the packages
# just packed above instead of whatever was last cached.
$sln = Join-Path $SolutionRoot "YouVersionPlatform.slnx"

Write-Step "Restoring solution"
dotnet restore $sln --force
Assert-LastExitCode "dotnet restore $sln"

Write-Step "Building solution ($Configuration)"
dotnet build $sln --configuration $Configuration --no-restore
Assert-LastExitCode "dotnet build $sln"

# ── Done ─────────────────────────────────────────────────────────────────────
Write-Host "`n  All packages rebuilt and the solution built successfully." -ForegroundColor Green
Write-Host "  Local feed : $LocalFeed" -ForegroundColor Gray
