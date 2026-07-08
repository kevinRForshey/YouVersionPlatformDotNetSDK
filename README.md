# Simple YouVersion Platform SDK

A .NET 10 SDK for the [YouVersion Platform REST API](https://developers.youversion.com), built as
a set of layered, independently-publishable NuGet packages plus a Blazor Server sample app.

## Projects

| Project | What it is | Docs |
|---|---|---|
| `Platform.API.Models` | Zero-dependency POCO models (versions, books, chapters, verses, passages, highlights, the bible index) | [README](Platform.API.Models/README.md) |
| `Platform.API` | Typed HTTP client SDK — Bible discovery, passages, highlights, OAuth 2.0 + PKCE, caching, resilience, rate limiting | [README](Platform.API/README.md) |
| `Platform.SDK.Services` | Business-logic layer between the raw HTTP client and the Blazor components | [README](Platform.SDK.Services/README.md) |
| `Platform.SDK.Components` | Blazor UI components (Bible reader, passage display) built on Fluent UI | [README](Platform.SDK.Components/README.md) |
| `YouVersion.UsfmReferences` | USFM scripture reference parsing/validation — a C# port of `youversion/usfm-references` | — |
| `PlatformTestApp` | Blazor Server sample app exercising the full stack, including a per-user OAuth token storage pattern | — |
| `Platform.API.Tests` | Unit tests for `Platform.API` (xUnit, FluentAssertions, Moq) plus an architecture fitness test enforcing the client-boundary rule below | — |

All projects target `net10.0`.

## Architecture

Layering is one-directional and enforced by an automated test
(`Platform.API.Tests/Architecture/ApiClientBoundaryTests.cs`), not just convention:

```
Platform.API.Models  →  Platform.API  →  Platform.SDK.Services  →  Platform.SDK.Components  →  PlatformTestApp
```

`PlatformTestApp` and `Platform.SDK.Components` are not permitted to reference `Platform.API`
clients directly — everything goes through `Platform.SDK.Services`.

## Quick start

```bash
dotnet add package YouVersion.Platform.API
dotnet add package YouVersion.Platform.API.Models
```

```csharp
builder.Services.AddYouVersionApiClients(options =>
{
    options.AppKey = builder.Configuration["YouVersionApi:AppKey"]!;
});
```

See [`Platform.API/README.md`](Platform.API/README.md) for full setup, including OAuth and
multi-user token storage.

## Building and testing

```bash
dotnet build YouVersionPlatform.slnx
dotnet test Platform.API.Tests
```

## Status

Under active development. Core Bible discovery, passage retrieval, highlight read/write, and OAuth
are implemented and tested. Not yet in place for a first stable release: CI build/test gates on
every push/PR, dedicated test coverage for `Platform.SDK.Services`, `Platform.SDK.Components`, and
`YouVersion.UsfmReferences`, and CI-driven package versioning.

## License

MIT — see [LICENSE](LICENSE).
