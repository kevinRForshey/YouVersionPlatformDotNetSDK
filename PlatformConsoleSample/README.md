# PlatformConsoleSample

A minimal console sample showing how to consume the [Bible Platform SDK for .NET](../README.md)
without any UI framework — just `Platform.API` + `Platform.API.Models` wired up on a plain
[.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host), for backend
services, scripts, Azure Functions, etc.

Unlike [`PlatformTestApp`](../PlatformTestApp/README.md) (a Blazor Server app showing the full
component/OAuth stack), this sample only exercises the raw API clients: listing Bible versions,
listing a version's books, and fetching a single passage.

## Running it

Prerequisites:

- .NET 10 SDK
- An app key from the [platform's developer portal](https://developers.youversion.com)

Configure the app key with [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
rather than committing it to source (see [`Platform.API/README.md` §
Configuration & secrets](../Platform.API/README.md#configuration--secrets) for the full rationale):

```bash
cd PlatformConsoleSample
dotnet user-secrets init
dotnet user-secrets set "BibleApi:AppKey" "YOUR_APP_KEY"
```

Then run it:

```bash
dotnet run --project PlatformConsoleSample
```

## What it demonstrates

| Call | API |
|---|---|
| List Bible versions | `IBibleClient.GetVersionsAsync` |
| List a version's books | `IBibleClient.GetBooksAsync` |
| Fetch a passage | `IPassageClient.GetPassageAsync`, using `Reference.FromString` from `BiblePlatform.UsfmReferences` |

DI wiring is a single call — `AddBibleApiClients(builder.Configuration)` — since this sample
doesn't need OAuth or caching. See [`Platform.API/README.md`](../Platform.API/README.md) for the
full set of `AddBible*` extensions (OAuth, caching, resilience) available when you need them.

## License

Apache License 2.0 — see [LICENSE](../LICENSE).
