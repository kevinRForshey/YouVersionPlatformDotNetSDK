# YouVersionPlatformDotNetSDK — Compliance & Relicensing Tasks

## Context (for whoever/whatever implements this)

YouVersion's Developer Terms of Use restrict distributing/publishing/sublicensing their
"YV IP" to third parties without prior written approval, and restrict use of their Marks
("YouVersion," "Life.Church," etc.) without explicit permission. This repo is being
repositioned from "installable SDK" to "public reference implementation, clone-and-build
only" until (or unless) written permission is obtained from YouVersion to distribute it as
an actual SDK. NuGet packages have already been unlisted manually — that step is done.
Everything below is what's left.

Work through this in order. Follow the plan-first / validate-before-done workflow — flag
anything ambiguous rather than guessing.

---

## 1. Remove YouVersion/Life.Church branding references

- [ ] Audit repo-wide for "YouVersion," "Life.Church," "YVP," and "The Bible App" —
      `grep -rniE "youversion|life\.?church|\bYVP\b" --include="*.cs" --include="*.md" --include="*.json" --include="*.csproj" .`
      excluding `bin/`, `obj/`, `.git/`.
- [ ] Rename NuGet `PackageId`s to drop "YouVersion" (all five `.csproj` files:
      `Platform.API.Models`, `Platform.API`, `Platform.SDK.Services`, `Platform.SDK.Components`,
      `YouVersion.UsfmReferences`). `YouVersion.UsfmReferences` needs a new project/namespace
      name too since "YouVersion" is in the project name itself — pick something neutral
      (e.g. `UsfmReferences` or `ScriptureReferences`) and confirm with Kevin before renaming
      the actual directory/namespace, since that's a bigger diff than a metadata change.
- [ ] Update `README.md`, all per-project `README.md` files, `CONTRIBUTING.md`, `SECURITY.md`,
      and `docs/**` to remove branded references, replacing with generic language ("the
      Platform API," "the upstream API," etc.) where the API itself still needs to be
      described functionally.
- [ ] Do **not** rename C# types/namespaces that just describe domain concepts already
      genericized (e.g. `PassageService`, `HighlightService`) — only touch things that
      actually contain the Marks.
- [ ] Leave a single, factual functional description of what API this integrates with
      (needed for the code to make sense) — the goal is removing *branding/marks*, not
      pretending the target platform doesn't exist.

## 2. Relicense MIT → Apache 2.0

- [ ] Replace `LICENSE` with standard Apache 2.0 text (apache.org/licenses/LICENSE-2.0.txt),
      copyright holder = Kevin Forshey, year = 2026.
- [ ] Add a `NOTICE` file at repo root containing the reference-implementation disclaimer
      (see section 3) — this is the Apache-standard home for that language and travels
      with the code on redistribution.
- [ ] Update `<PackageLicenseExpression>` from `MIT` to `Apache-2.0` in every `.csproj`
      that currently sets it (check `Directory.Build.props` first in case it's set once
      solution-wide).
- [ ] Update the license badge and link in `README.md`.
- [ ] Add an entry to `CHANGELOG.md` under `## [Unreleased]` noting the relicense.

## 3. Add reference-implementation disclaimer

- [ ] Add a banner at the very top of `README.md` (after the title, before the CI badge is
      fine) stating this is a reference implementation demonstrating integration patterns,
      not a distributed/installable SDK, not affiliated with or endorsed by the platform
      owner, and that using the underlying API requires the reader's own developer
      credentials and acceptance of the platform owner's own terms.
- [ ] Put the same substance in `NOTICE` (required content, not just README — NOTICE is
      the part that's supposed to survive forks/copies).
- [ ] Keep the "Which package(s) do I need?" and quickstart sections in the README, but
      reframe the install instructions as "clone this repo and build/reference the project
      locally" instead of `dotnet add package`.

## 4. Remove the NuGet publishing path

- [ ] Delete `.github/workflows/nuget-publish.yml` — no packages should be pushed to
      NuGet.org going forward until/unless this is revisited.
- [ ] Strip NuGet-publish-only metadata from the five library `.csproj` files that no
      longer applies now that these aren't published packages: `<PackageId>`, `<Authors>`,
      `<PackageReadmeFile>`, `<PackageIcon>`, `<PackageLicenseExpression>` stays (still
      correct/desirable even for source-only use). Keep `<GenerateDocumentationFile>`,
      `<IncludeSymbols>`/SourceLink config — those are fine and useful for local
      consumption via project reference.
- [ ] Leave `ci.yml` alone — build/test/pack-to-local-feed is still the right CI shape for
      validating the code; only the *publish-to-NuGet.org* step is the problem, and that
      lives entirely in the workflow being deleted above.
- [ ] Remove or update `Publish-NuGetRelease.ps1` / `publish-nuget-release.sh` /
      `Rebuild-Packages.ps1` / `rebuild-nuget-packages.sh` at the repo root — confirm with
      Kevin whether to delete these outright or leave them with a header comment noting
      they're currently unused pending YouVersion's response, since deleting loses the
      work if permission comes through later.
- [ ] Update `README.md`'s "Versioning & Releases" section to remove the "publish to
      NuGet.org" framing and describe tags as marking stable reference points in the repo
      instead.

## 5. Documentation sweep

- [ ] Update `CONTRIBUTING.md` and `SECURITY.md` — both currently describe a "published
      package" support model; adjust language to match "reference implementation, no
      published artifact."
- [ ] Update `docs/getting-started.md` and `docs/authentication.md` install snippets to
      match the clone-and-build instructions from section 3.

## Explicitly out of scope for this pass

- Do not change `Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs` or the
  layering rules — none of this affects the architecture, only distribution/branding.
- Do not touch actual OAuth/PKCE, caching, or rate-limiting logic.
- Do not delete git tags `v0.1.0`–`v0.1.4` — leave release history as-is.
