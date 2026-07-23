# 4. "Unofficial" package naming and git-tag-derived versioning

## Status

Accepted, package naming since superseded — see "Update" below.

## Update

The `.Unofficial`-suffixed, `YouVersion.*`-rooted naming described in this ADR was later dropped
entirely: `PackageId`s no longer reference the `YouVersion` mark at all (e.g.
`YouVersion.Platform.API.Unofficial` → `BiblePlatform.API`, `YouVersion.UsfmReferences.Unofficial`
→ `BiblePlatform.UsfmReferences`), so there's nothing left to disclaim in the identifier itself.
The MinVer-derived versioning decision below is unaffected by that rename. This ADR is kept as the
historical record of the `.Unofficial` naming's reasoning, not as a description of the current
`PackageId`s.

## Context

This SDK is an independent, community project against YouVersion's public Platform API — it is not
published or endorsed by YouVersion / Life.Church. A package named e.g. `YouVersion.Platform.API`
without qualification could be mistaken for an official, vendor-published package, which is both a
trademark concern and misleading to consumers about who supports it.

Separately, five packages ship from this repo (`Platform.API.Models`, `Platform.API`,
`Platform.SDK.Services`, `Platform.SDK.Components`, `YouVersion.UsfmReferences`) and hand-maintaining a
version number per `.csproj`, kept in sync across all five on every release, is manual bookkeeping that
drifts the moment someone forgets to bump one.

## Decision

Every package that wraps YouVersion's API carries an explicit `.Unofficial` suffix on its `PackageId`
(e.g. `YouVersion.Platform.API.Unofficial`) — chosen as a suffix rather than a prefix
(`Unofficial-YouVersion.X`, an earlier naming this repo used) so the packages still sort and group
together in a package list by their `YouVersion.*` namespace root.

Versions for the four `Platform.*` packages are derived automatically from git tags via
[MinVer](https://github.com/adamralph/minver) (`Directory.Build.props`, `MinVerTagPrefix = "v"`):
tagging a commit `vX.Y.Z` and pushing it is the only manual step; untagged commits build as
`X.Y.(Z+1)-alpha.0.N` prereleases automatically, so every local or CI build produces a valid,
monotonically increasing version with no hand-edited `<Version>` element.

`YouVersion.UsfmReferences.Unofficial` is the exception: it carries an explicit `<Version>` instead of a
MinVer-derived one, because it tracks the upstream `youversion/usfm-references` Python library's release
cadence independently of the `Platform.*` packages' own release cycle.

## Consequences

- The four MinVer-versioned packages cannot ship a version that wasn't produced by a corresponding git
  tag — there is no path to accidentally hand-editing a version number that then diverges from the tag
  history.
- `EnablePackageValidation` is currently disabled repo-wide: it compares a new package against the last
  published baseline under the *same* `PackageId`, and the prefix-to-suffix rename means no baseline
  exists yet under the new IDs. It should be re-enabled once the first release under the `.Unofficial`
  suffix convention is published, pointing `PackageValidationBaselineVersion` at that release.
- `YouVersion.UsfmReferences.Unofficial` versioning independently of the other four means a consumer
  installing multiple packages from this repo cannot assume matching version numbers imply matching
  release dates for that one package.
