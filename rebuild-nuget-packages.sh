#!/usr/bin/env bash
#
# Rebuilds the NuGet packages produced by this solution and drops them in the
# local feed (./packages, see nuget.config) so PlatformTestApp and other
# consumers can pick up fresh builds.
#
# Usage:
#   ./rebuild-nuget-packages.sh [-c Configuration] [--clean] [--no-cache-clear] [--skip-tests]
#
#   -c, --configuration   Build configuration (default: Release)
#   --clean               Remove ./packages before packing
#   --no-cache-clear      Skip purging these packages from ~/.nuget/packages
#                         (by default the cache is purged so a rebuild with an
#                         unchanged <Version> is actually picked up by restore)
#   --skip-tests          Skip `dotnet test`

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

CONFIGURATION="Release"
CLEAN=false
CLEAR_CACHE=true
RUN_TESTS=true
OUTPUT_DIR="$SCRIPT_DIR/packages"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --clean)
      CLEAN=true
      shift
      ;;
    --no-cache-clear)
      CLEAR_CACHE=false
      shift
      ;;
    --skip-tests)
      RUN_TESTS=false
      shift
      ;;
    -h|--help)
      grep '^#' "$0" | sed 's/^#//'
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

# Pack in dependency order so ProjectReferences resolve cleanly.
# project dir -> nuget package id (id needed to bust the global cache below)
PROJECTS=(
  "Platform.API.Models:YouVersion.Platform.API.Models"
  "YouVersion.UsfmReferences:YouVersion.UsfmReferences"
  "Platform.API:YouVersion.Platform.API"
  "Platform.SDK.Services:YouVersion.Platform.SDK.Services"
  "Platform.SDK.Components:YouVersion.Platform.SDK.Components"
)

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$1"; }

if $CLEAN; then
  log "Removing $OUTPUT_DIR"
  rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

if $CLEAR_CACHE; then
  log "Purging affected packages from local NuGet cache (so unchanged versions still refresh)"
  GLOBAL_PACKAGES_DIR="$(dotnet nuget locals global-packages --list | sed 's/^global-packages: //')"
  for entry in "${PROJECTS[@]}"; do
    package_id="${entry#*:}"
    package_dir="${GLOBAL_PACKAGES_DIR}${package_id,,}"
    if [[ -d "$package_dir" ]]; then
      echo "  removing $package_dir"
      rm -rf "$package_dir"
    fi
  done
fi

# Note: we deliberately build/pack each project directly instead of running
# `dotnet restore`/`build` against YouVersionPlatform.slnx, since PlatformTestApp
# consumes YouVersion.Platform.SDK.Components as a packed NuGet package (see the
# "YouVersionLocal" feed in nuget.config) and would fail to restore before that
# feed is populated by the packing loop below.
for entry in "${PROJECTS[@]}"; do
  project_dir="${entry%%:*}"
  log "Restoring & building $project_dir ($CONFIGURATION)"
  dotnet build "$project_dir" -c "$CONFIGURATION"
done

if $RUN_TESTS && [[ -d "$SCRIPT_DIR/Platform.API.Tests" ]]; then
  log "Running Platform.API.Tests"
  dotnet test "Platform.API.Tests" -c "$CONFIGURATION"
fi

if $RUN_TESTS && [[ -d "$SCRIPT_DIR/YouVersion.UsfmReferences.Tests" ]]; then
  log "Running YouVersion.UsfmReferences.Tests"
  dotnet test "YouVersion.UsfmReferences.Tests" -c "$CONFIGURATION"
fi

for entry in "${PROJECTS[@]}"; do
  project_dir="${entry%%:*}"
  log "Packing $project_dir"
  dotnet pack "$project_dir" -c "$CONFIGURATION" --no-build -o "$OUTPUT_DIR"
done

log "Done. Packages written to $OUTPUT_DIR"
ls -1 "$OUTPUT_DIR"/*.nupkg
