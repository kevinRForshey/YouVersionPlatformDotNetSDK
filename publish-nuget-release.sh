#!/usr/bin/env bash
#
# Cuts a tagged release that triggers the existing GitHub Actions publish workflow.
#
# This script does NOT push packages to NuGet.org itself -- that's done by
# .github/workflows/nuget-publish.yml, which runs on GitHub's runners using the
# NUGET_API_KEY repo secret. What this script automates is everything that leads
# up to that: preflight checks, an optional local build/test/pack dry run (to
# catch problems before anything touches the remote), tagging, pushing the tag,
# and creating the GitHub Release that fires the workflow.
#
# Usage:
#   ./publish-nuget-release.sh -v 0.1.1 [options]
#
#   -v, --version VERSION     Required. e.g. 0.1.1 (no "v" prefix). Becomes tag
#                              "v0.1.1" and, via MinVer, the version of all 4
#                              MinVer-driven packages.
#   --remote REMOTE            Git remote to push the tag to. Default: origin
#   --branch BRANCH            Branch that must be checked out and in sync with
#                              <remote>. Default: main
#   --notes-file FILE          Changelog file to pull the "## [Unreleased]"
#                              section from for release notes. Default: CHANGELOG.md
#   --skip-local-build         Skip the local restore/build/test/pack verification pass.
#   --dry-run                  Run all checks (and the local build, unless
#                              --skip-local-build), print exactly what would be
#                              tagged/pushed/released, and stop.
#   -y, --yes                  Skip the confirmation prompt before tagging/pushing/releasing.
#   -h, --help                 Show this help.
#
# Examples:
#   ./publish-nuget-release.sh -v 0.1.1
#   ./publish-nuget-release.sh -v 0.1.1 --dry-run
#   ./publish-nuget-release.sh -v 0.1.1 --skip-local-build --yes

set -euo pipefail

VERSION=""
REMOTE="origin"
BRANCH="main"
NOTES_FILE="CHANGELOG.md"
SKIP_LOCAL_BUILD=false
DRY_RUN=false
ASSUME_YES=false

usage() {
    grep '^#' "$0" | sed 's/^#//' | sed '1d'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -v|--version) VERSION="$2"; shift 2 ;;
        --remote) REMOTE="$2"; shift 2 ;;
        --branch) BRANCH="$2"; shift 2 ;;
        --notes-file) NOTES_FILE="$2"; shift 2 ;;
        --skip-local-build) SKIP_LOCAL_BUILD=true; shift ;;
        --dry-run) DRY_RUN=true; shift ;;
        -y|--yes) ASSUME_YES=true; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage; exit 1 ;;
    esac
done

if [[ -z "$VERSION" ]]; then
    echo "Error: -v/--version X.Y.Z is required." >&2
    usage
    exit 1
fi
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Error: version must look like X.Y.Z (got: $VERSION)" >&2
    exit 1
fi
TAG="v$VERSION"

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$1"; }
warn() { printf '\033[1;33m%s\033[0m\n' "$1"; }
ok() { printf '\033[1;32m%s\033[0m\n' "$1"; }
die() { printf '\033[1;31m%s\033[0m\n' "$1" >&2; exit 1; }

confirm() {
    $ASSUME_YES && return 0
    read -r -p "$1 [y/N] " reply
    [[ "$reply" =~ ^[Yy]$ ]] || die "Aborted."
}

# ── 0. Locate repo root ───────────────────────────────────────────────────────
REPO_ROOT="$(git rev-parse --show-toplevel)" || die "Not inside a git repository."
cd "$REPO_ROOT"

# ── 1. Prerequisites ──────────────────────────────────────────────────────────
log "Checking prerequisites"
for cmd in git gh dotnet; do
    command -v "$cmd" >/dev/null 2>&1 || die "'$cmd' is required on PATH."
done
gh auth status >/dev/null 2>&1 || die "gh CLI is not authenticated. Run 'gh auth login' first."
echo "    git, gh, dotnet found; gh is authenticated."

# ── 2. Working tree / branch state ────────────────────────────────────────────
log "Checking working tree and branch state"

status_output="$(git status --porcelain)"
if [[ -n "$status_output" ]]; then
    echo "$status_output"
    die "Working tree is not clean. Commit or stash changes first."
fi

current_branch="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$current_branch" != "$BRANCH" ]]; then
    die "On branch '$current_branch', expected '$BRANCH'. Switch branches or pass --branch."
fi

git fetch "$REMOTE" --tags

local_head="$(git rev-parse HEAD)"
remote_head="$(git rev-parse "$REMOTE/$BRANCH")"
if [[ "$local_head" != "$remote_head" ]]; then
    die "Local '$BRANCH' (${local_head:0:7}) differs from '$REMOTE/$BRANCH' (${remote_head:0:7}). Push or pull first."
fi
echo "    Clean, on '$BRANCH', in sync with '$REMOTE/$BRANCH' (${local_head:0:7})."

# ── 3. Tag collision checks ───────────────────────────────────────────────────
log "Checking tag '$TAG' doesn't already exist"
if git tag -l "$TAG" | grep -qx "$TAG"; then
    die "Tag $TAG already exists locally."
fi
if git ls-remote --tags "$REMOTE" "refs/tags/$TAG" | grep -q "$TAG"; then
    die "Tag $TAG already exists on $REMOTE."
fi
echo "    $TAG is free locally and on $REMOTE."

# ── 4. Local verification build (never touches the remote or NuGet.org) ──────
if ! $SKIP_LOCAL_BUILD; then
    log "Verifying build/test/pack locally"

    # nuget.config's "YouVersionLocal" source must exist at restore time.
    mkdir -p "$REPO_ROOT/packages"

    PROJECTS=(
        "Platform.API.Models"
        "YouVersion.UsfmReferences"
        "Platform.API"
        "Platform.SDK.Services"
        "Platform.SDK.Components"
    )
    SCRATCH="$(mktemp -d)"
    trap 'rm -rf "$SCRATCH"' EXIT

    dotnet restore
    dotnet build -c Release --no-restore
    dotnet test -c Release --no-build --verbosity normal

    for p in "${PROJECTS[@]}"; do
        echo "    packing $p"
        dotnet pack "$p" -c Release --no-build -o "$SCRATCH"
    done
    ok "    Local verification succeeded. Packages: $SCRATCH"
else
    warn "Skipping local build/test/pack verification (--skip-local-build)"
fi

# ── 5. Release notes from CHANGELOG.md's "## [Unreleased]" section ───────────
notes="Release $TAG"
if [[ -f "$REPO_ROOT/$NOTES_FILE" ]]; then
    extracted="$(awk '/^## \[Unreleased\]/{flag=1; next} /^## \[/{flag=0} flag' "$REPO_ROOT/$NOTES_FILE")"
    trimmed="$(printf '%s' "$extracted" | sed '/^[[:space:]]*$/d')"
    if [[ -n "$trimmed" ]]; then
        notes="$extracted"
    fi
fi

# ── 6. Summary + confirmation ─────────────────────────────────────────────────
log "About to publish"
echo "  Tag:      $TAG"
echo "  Branch:   $BRANCH (${local_head:0:7})"
echo "  Remote:   $REMOTE"
echo "  Notes:"
echo "$notes" | sed 's/^/    /'
echo
echo "  This tags + pushes '$TAG' and publishes a GitHub Release against it,"
echo "  which triggers .github/workflows/nuget-publish.yml to build, test, pack,"
echo "  and push all 5 packages to NuGet.org."

if $DRY_RUN; then
    warn $'\nDry run — no tag pushed, no release created. Commands that would run:'
    echo "  git tag -a $TAG -m \"$TAG\""
    echo "  git push $REMOTE $TAG"
    echo "  gh release create $TAG --title \"$TAG\" --notes-file <tmp>"
    exit 0
fi

confirm $'\nCreate tag '"$TAG"', push it, and publish a GitHub Release?'

# ── 7. Tag, push, release ─────────────────────────────────────────────────────
log "Tagging $TAG"
git tag -a "$TAG" -m "$TAG"

log "Pushing tag to $REMOTE"
git push "$REMOTE" "$TAG"

log "Creating GitHub Release $TAG"
notes_file="$(mktemp)"
printf '%s\n' "$notes" > "$notes_file"
if ! gh release create "$TAG" --title "$TAG" --notes-file "$notes_file"; then
    rm -f "$notes_file"
    die "gh release create failed. The tag was already pushed — check 'gh release list' before retrying."
fi
rm -f "$notes_file"

# ── 8. Watch the triggered workflow run ───────────────────────────────────────
log "Watching nuget-publish.yml"
run_id=""
for _ in $(seq 1 12); do
    run_id="$(gh run list --workflow=nuget-publish.yml --event=release -L 1 --json databaseId --jq '.[0].databaseId' 2>/dev/null || true)"
    [[ -n "$run_id" ]] && break
    sleep 5
done

if [[ -n "$run_id" ]]; then
    if gh run watch "$run_id" --exit-status; then
        ok "
Published $TAG successfully."
    else
        die "
Workflow failed — check: gh run view $run_id --log-failed"
    fi
else
    warn "Could not find the triggered run automatically. Check the Actions tab or:"
    echo "  gh run list --workflow=nuget-publish.yml"
fi
