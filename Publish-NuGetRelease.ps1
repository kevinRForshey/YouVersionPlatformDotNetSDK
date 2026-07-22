<#
.SYNOPSIS
    Cuts a tagged release that triggers the existing GitHub Actions publish workflow.

.DESCRIPTION
    This is the source of truth for cutting a release, not a manual fallback --
    nuget-publish.yml only ever runs as a result of the GitHub Release this script
    creates. It does NOT push packages to NuGet.org itself; that's done by
    .github/workflows/nuget-publish.yml, which runs on GitHub's runners using the
    NUGET_API_KEY repo secret. What this script automates is everything that leads
    up to that: preflight checks, an optional local build/test/pack dry run (to
    catch problems before anything touches the remote), tagging, pushing the tag,
    and creating the GitHub Release that fires the workflow.

    Rebuild-Packages.ps1 / rebuild-nuget-packages.sh are unrelated: they repack
    projects into the local NuGet feed for day-to-day development and have no
    connection to tagging, GitHub Releases, or nuget-publish.yml.

    Steps:
      1. Verify prerequisites (git/gh/dotnet on PATH, gh authenticated).
      2. Verify the working tree is clean and local <Branch> matches <Remote>/<Branch>.
      3. Verify the target tag doesn't already exist locally or on the remote.
      4. Unless -SkipLocalBuild, restore/build/test the solution and pack all 5
         projects into a scratch folder — purely local verification.
      5. Pull release notes from CHANGELOG.md's "## [Unreleased]" section (or fall
         back to a generic message).
      6. Show a summary and prompt for confirmation (unless -Yes).
      7. git tag, git push the tag, gh release create.
      8. Watch the triggered nuget-publish.yml run and report success/failure.

.PARAMETER Version
    Version to release, e.g. "0.1.1" (no "v" prefix). Becomes tag "v0.1.1" and,
    via MinVer, the version of all 4 MinVer-driven packages.

.PARAMETER Remote
    Git remote to push the tag to. Defaults to "origin".

.PARAMETER Branch
    Branch that must be checked out and up to date with <Remote>. Defaults to "main".

.PARAMETER NotesFile
    Changelog file to pull the "## [Unreleased]" section from for release notes.
    Defaults to "CHANGELOG.md".

.PARAMETER SkipLocalBuild
    Skip the local restore/build/test/pack verification pass.

.PARAMETER DryRun
    Run all checks and the local build (unless -SkipLocalBuild), print exactly what
    would be tagged/pushed/released, and stop — no tag, push, or release is created.

.PARAMETER Yes
    Skip the confirmation prompt before tagging/pushing/releasing.

.EXAMPLE
    ./Publish-NuGetRelease.ps1 -Version 0.1.1

.EXAMPLE
    ./Publish-NuGetRelease.ps1 -Version 0.1.1 -DryRun

.EXAMPLE
    ./Publish-NuGetRelease.ps1 -Version 0.1.1 -SkipLocalBuild -Yes
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Remote = "origin",
    [string]$Branch = "main",
    [string]$NotesFile = "CHANGELOG.md",
    [switch]$SkipLocalBuild,
    [switch]$DryRun,
    [switch]$Yes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Tag = "v$Version"

function Write-Step([string]$msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

function Assert-LastExitCode([string]$label) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $label (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

function Confirm-OrAbort([string]$prompt) {
    if ($Yes) { return }
    $resp = Read-Host "$prompt [y/N]"
    if ($resp -notmatch '^[Yy]') {
        Write-Host "Aborted." -ForegroundColor Yellow
        exit 1
    }
}

# ── 0. Locate repo root ───────────────────────────────────────────────────────
$RepoRoot = (git rev-parse --show-toplevel 2>$null)
if (-not $RepoRoot) { throw "Not inside a git repository." }
Set-Location $RepoRoot

# ── 1. Prerequisites ──────────────────────────────────────────────────────────
Write-Step "Checking prerequisites"
foreach ($cmd in @('git', 'gh', 'dotnet')) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "'$cmd' is required on PATH."
    }
}
gh auth status *> $null
if ($LASTEXITCODE -ne 0) {
    throw "gh CLI is not authenticated. Run 'gh auth login' first."
}
Write-Host "    git, gh, dotnet found; gh is authenticated."

# ── 2. Working tree / branch state ────────────────────────────────────────────
Write-Step "Checking working tree and branch state"

$statusOutput = git status --porcelain
if ($statusOutput) {
    Write-Host $statusOutput
    throw "Working tree is not clean. Commit or stash changes first."
}

$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne $Branch) {
    throw "On branch '$currentBranch', expected '$Branch'. Switch branches or pass -Branch."
}

git fetch $Remote --tags | Out-Null
Assert-LastExitCode "git fetch $Remote"

$localHead = git rev-parse HEAD
$remoteHead = git rev-parse "$Remote/$Branch"
if ($localHead -ne $remoteHead) {
    throw "Local '$Branch' ($($localHead.Substring(0,7))) differs from '$Remote/$Branch' ($($remoteHead.Substring(0,7))). Push or pull first."
}
Write-Host "    Clean, on '$Branch', in sync with '$Remote/$Branch' ($($localHead.Substring(0,7)))."

# ── 3. Tag collision checks ───────────────────────────────────────────────────
Write-Step "Checking tag '$Tag' doesn't already exist"
if (git tag -l $Tag) {
    throw "Tag $Tag already exists locally."
}
if (git ls-remote --tags $Remote $Tag) {
    throw "Tag $Tag already exists on $Remote."
}
Write-Host "    $Tag is free locally and on $Remote."

# ── 4. Local verification build (never touches the remote or NuGet.org) ──────
if (-not $SkipLocalBuild) {
    Write-Step "Verifying build/test/pack locally"

    # nuget.config's "YouVersionLocal" source must exist at restore time.
    New-Item -ItemType Directory -Force -Path (Join-Path $RepoRoot "packages") | Out-Null

    $projects = @(
        "Platform.API.Models",
        "YouVersion.UsfmReferences",
        "Platform.API",
        "Platform.SDK.Services",
        "Platform.SDK.Components"
    )
    $scratch = Join-Path ([IO.Path]::GetTempPath()) "nuget-publish-verify-$Version"
    if (Test-Path $scratch) { Remove-Item $scratch -Recurse -Force }
    New-Item -ItemType Directory -Path $scratch | Out-Null

    dotnet restore
    Assert-LastExitCode "dotnet restore"

    dotnet build -c Release --no-restore
    Assert-LastExitCode "dotnet build"

    dotnet test -c Release --no-build --verbosity normal
    Assert-LastExitCode "dotnet test"

    foreach ($p in $projects) {
        Write-Host "    packing $p"
        dotnet pack $p -c Release --no-build -o $scratch
        Assert-LastExitCode "dotnet pack $p"
    }
    Write-Host "    Local verification succeeded. Packages: $scratch" -ForegroundColor Green
}
else {
    Write-Host "Skipping local build/test/pack verification (-SkipLocalBuild)" -ForegroundColor Yellow
}

# ── 5. Release notes from CHANGELOG.md's "## [Unreleased]" section ───────────
$notes = "Release $Tag"
$notesPath = Join-Path $RepoRoot $NotesFile
if (Test-Path $notesPath) {
    $capture = $false
    $body = New-Object System.Collections.Generic.List[string]
    foreach ($line in Get-Content $notesPath) {
        if ($line -match '^## \[Unreleased\]') { $capture = $true; continue }
        if ($capture -and $line -match '^## \[') { break }
        if ($capture) { $body.Add($line) }
    }
    $bodyText = ($body -join "`n").Trim()
    if ($bodyText) { $notes = $bodyText }
}

# ── 6. Summary + confirmation ─────────────────────────────────────────────────
Write-Step "About to publish"
Write-Host "  Tag:      $Tag"
Write-Host "  Branch:   $Branch ($($localHead.Substring(0,7)))"
Write-Host "  Remote:   $Remote"
Write-Host "  Notes:"
($notes -split "`n") | ForEach-Object { Write-Host "    $_" }
Write-Host ""
Write-Host "  This tags + pushes '$Tag' and publishes a GitHub Release against it," -ForegroundColor Gray
Write-Host "  which triggers .github/workflows/nuget-publish.yml to build, test, pack," -ForegroundColor Gray
Write-Host "  and push all 5 packages to NuGet.org." -ForegroundColor Gray

if ($DryRun) {
    Write-Host "`nDry run — no tag pushed, no release created. Commands that would run:" -ForegroundColor Yellow
    Write-Host "  git tag -a $Tag -m `"$Tag`""
    Write-Host "  git push $Remote $Tag"
    Write-Host "  gh release create $Tag --title `"$Tag`" --notes-file <tmp>"
    exit 0
}

Confirm-OrAbort "`nCreate tag $Tag, push it, and publish a GitHub Release?"

# ── 7. Tag, push, release ─────────────────────────────────────────────────────
Write-Step "Tagging $Tag"
git tag -a $Tag -m $Tag
Assert-LastExitCode "git tag"

Write-Step "Pushing tag to $Remote"
git push $Remote $Tag
Assert-LastExitCode "git push $Remote $Tag"

Write-Step "Creating GitHub Release $Tag"
$notesFile = New-TemporaryFile
Set-Content -Path $notesFile -Value $notes
gh release create $Tag --title $Tag --notes-file $notesFile
$releaseExit = $LASTEXITCODE
Remove-Item $notesFile -Force
if ($releaseExit -ne 0) {
    throw "gh release create failed (exit $releaseExit). The tag was already pushed — check 'gh release list' before retrying."
}

# ── 8. Watch the triggered workflow run ───────────────────────────────────────
Write-Step "Watching nuget-publish.yml"
$runId = $null
for ($i = 0; $i -lt 12; $i++) {
    $runId = gh run list --workflow=nuget-publish.yml --event=release -L 1 --json databaseId --jq '.[0].databaseId' 2>$null
    if ($runId) { break }
    Start-Sleep -Seconds 5
}

if ($runId) {
    gh run watch $runId --exit-status
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nPublished $Tag successfully." -ForegroundColor Green
    }
    else {
        Write-Host "`nWorkflow failed — check: gh run view $runId --log-failed" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "Could not find the triggered run automatically. Check the Actions tab or:" -ForegroundColor Yellow
    Write-Host "  gh run list --workflow=nuget-publish.yml"
}
