# Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) checks whether the test
suites actually assert on behavior, not just execute it: it makes small deliberate changes
("mutants") to the source — flipping a `>` to `>=`, deleting a null-check, changing a boundary
constant — and reruns the tests against each one. A mutant that no test fails on ("survives")
marks a gap: the code path ran, but nothing verified its result.

It is set up as a [local .NET tool](../.config/dotnet-tools.json) (`dotnet tool restore` pulls it
in) rather than a CI gate — a full mutation run is much slower than the regular test suite, so it's
a periodic/manual check, not something that blocks every push.

## Running it

```bash
dotnet tool restore
cd <Project>.Tests
dotnet dotnet-stryker
```

Each testable library has its own `stryker-config.json` in its `*.Tests` project directory (that's
also where the HTML report lands, under `StrykerOutput/`). The `project` key in each config picks
the source project to mutate out of the test project's references, since most test projects here
reference more than one project in the `Models -> API -> Services -> Components` chain.

## Coverage

| Project | Config | Status |
|---|---|---|
| `BiblePlatform.UsfmReferences` | [`BiblePlatform.UsfmReferences.Tests/stryker-config.json`](../BiblePlatform.UsfmReferences.Tests/stryker-config.json) | Runs. Baseline score ~41% — almost entirely `BookCatalog.cs`'s static USFM lookup tables, which are exercised but not asserted on exhaustively. |
| `Platform.SDK.Services` | [`Platform.SDK.Services.Tests/stryker-config.json`](../Platform.SDK.Services.Tests/stryker-config.json) | Runs. Baseline score ~96%. |
| `Platform.API` | [`Platform.API.Tests/stryker-config.json`](../Platform.API.Tests/stryker-config.json) | Runs. Baseline score ~48% — concentrated in `BibleOAuthClient.cs`, the caching client decorators, and the rate-limiting handler. |
| `Platform.SDK.Components` | [`Platform.SDK.Components.Tests/stryker-config.json`](../Platform.SDK.Components.Tests/stryker-config.json) | Does not currently run — Stryker.NET's rollback-on-compile-error step can't handle mutating an overridden Blazor lifecycle method (e.g. `OnParametersSetAsync`) in a `.razor.cs` code-behind partial class, and the whole run aborts with an internal compilation error. This is a known category of issue with Stryker.NET and Razor component code-behind, not something fixable from this repo's side. The config is left in place in case a future Stryker.NET release resolves it. |

Baseline scores above are a snapshot, not an enforced gate (`break` threshold is `0` in every
config) — treat a low score as a prioritization signal for which tests to strengthen, not a build
failure.
