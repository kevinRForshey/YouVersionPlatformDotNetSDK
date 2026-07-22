# Contributing

Thanks for considering a contribution to this SDK. It's a small, single-maintainer project, so a
few conventions keep changes easy to review.

## Before you start

For anything beyond a small fix (new features, breaking changes, new public API surface), open an
issue first to discuss the approach before writing code — it's easy to end up with a PR that
doesn't fit the existing architecture otherwise.

## Project layout

See the dependency chain and package descriptions in the top-level [README](README.md#packages).
The layering (`Models -> API -> Services -> Components -> PlatformTestApp`) is enforced by
`Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs` and
`ProjectDependencyDirectionTests.cs` — a PR that violates it will fail CI, not just review.

Non-obvious "why" behind this and other structural decisions (caching, rate limiting, package naming)
is recorded in [`docs/adr/`](docs/adr/README.md) — add a new ADR there for any decision of similar
weight rather than leaving the reasoning only in a PR description.

## Making a change

1. Fork and branch from `Development`.
2. Keep the change focused; unrelated formatting or refactoring makes review harder.
3. Add or update tests under the matching `*.Tests` project. `PlatformTestApp.Tests` is run in CI
   like every other test project — sample-app code isn't exempt from coverage.
4. Update `CHANGELOG.md`'s `## [Unreleased]` section for any user-visible change (new API, bug
   fix, behavior change). See existing entries for the expected level of detail.
5. Run the full build and test suite locally before opening a PR:

   ```bash
   dotnet build YouVersionPlatform.slnx
   dotnet test YouVersionPlatform.slnx
   ```

   For non-trivial test changes, consider also running [mutation testing](docs/mutation-testing.md)
   against the affected project — passing tests don't guarantee they'd catch a regression.

6. Open a PR against `Development` describing what changed and why. Link the issue it addresses,
   if any.

## Code style

- `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` are set
  solution-wide — a PR that introduces warnings won't build.
- Public members should carry XML doc comments (`GenerateDocumentationFile` is on for every
  shipping project).
- Match the existing style in the file you're editing rather than introducing a new one.

## Reporting a security issue

Do not open a public issue for a security vulnerability — see [SECURITY.md](SECURITY.md).
