#  Simple YouVersion Platform SDK
This is a test application to see what I can do to create a full (eventually) DotNet 10 SDK for the new YouVersion Platform.

## Versioning & Releases

`Platform.API`, `Platform.API.Models`, `Platform.SDK.Services`, and `Platform.SDK.Components` version
together in lockstep, driven by [MinVer](https://github.com/adamralph/minver) (see `Directory.Build.props`)
instead of hand-edited `<Version>` elements. The version is derived from the nearest git tag matching
`v*` (e.g. `v1.2.0`):

- Building/packing at a tagged commit produces exactly that version (`1.2.0`).
- Building/packing any other commit produces an automatic prerelease version based on commit height
  since the last tag (e.g. `1.2.1-alpha.0.3`), so local and CI builds never collide with a published
  version.

`YouVersion.UsfmReferences` is versioned independently (it tracks the upstream `usfm-references`
release number) and opts out via `MinVerSkip`.

**To cut a release:**

1. `git tag -a vX.Y.Z -m "..."` on the commit you want to ship, then `git push origin vX.Y.Z`.
2. Create a GitHub Release from that tag. Publishing the release triggers `.github/workflows/nuget-publish.yml`,
   which builds, tests, packs all four projects at that resolved version, and pushes them to NuGet.org.

No `.csproj` edits are required to bump versions.
