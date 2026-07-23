# Getting Started

This walks through installing the SDK and making your first read-only API call — listing Bible
versions and fetching a passage — with no sign-in required. For user sign-in (OAuth/PKCE), see the
[OAuth guide](oauth-guide.md) once this is working. For app key setup specifically, see
[Authentication (app key)](authentication.md).

## 1. Pick your project(s)

| If you want to... | Reference |
|---|---|
| Call the Platform API directly (versions, passages, highlights) from a backend, console app, Azure Function, etc. — no UI | `Platform.API.Models` + `Platform.API` |
| Build your own UI on top of ready-made business logic (highlight toggling, reader state) instead of raw HTTP calls | Add `Platform.SDK.Services` |
| Build a Blazor app with ready-made UI — pickers, `BibleReader`, click-to-highlight, the `BibleAuth` sign-in widget | `Platform.SDK.Components` (references `Services`, `API`, and `Models` transitively — just reference this one) |
| Parse or validate USFM scripture references, unrelated to the Platform API | `BiblePlatform.UsfmReferences` only |

This guide covers the first row — the typed HTTP client with no UI layer.

> **This SDK is not published as an installable package.** Clone this repository and reference
> the projects directly — see
> [Referencing this repo locally](../README.md#referencing-this-repo-locally) in the solution
> README.

```bash
dotnet add reference ../BiblePlatformDotNetSDK/Platform.API.Models/Platform.API.Models.csproj
dotnet add reference ../BiblePlatformDotNetSDK/Platform.API/Platform.API.csproj
```

## 2. Get an app key

Every call to the Platform API requires an app key, sent as the `X-YVP-App-Key` header. Register
an application at [developers.youversion.com](https://developers.youversion.com) to obtain one. See
[Authentication (app key)](authentication.md) for how the SDK uses it and how to store it safely.

## 3. Register the API clients

```csharp
builder.Services.AddBibleApiClients(options =>
{
    options.AppKey = builder.Configuration["BibleApi:AppKey"]!;
});
```

```json
{
  "BibleApi": {
    "AppKey": "YOUR_APP_KEY",
    "BaseAddress": "https://api.youversion.com",
    "Timeout": "00:00:30",
    "OutboundRequestsPerSecond": 10,
    "OutboundBurstSize": 20,
    "OutboundQueueLimit": 100
  }
}
```

This registers `IBibleClient`, `IPassageClient`, and `IHighlightClient` (highlight writes require a
signed-in user — see the [OAuth guide](oauth-guide.md) — but reads work with just the app key).

## 4. Make your first calls

List available Bible versions:

```csharp
public sealed class VersionReader(IBibleClient bibleClient)
{
    public Task<PagedResult<BibleVersionSummary>> GetEnglishVersionsAsync(CancellationToken ct)
        => bibleClient.GetVersionsAsync(languageRange: "en", cancellationToken: ct);
}
```

Fetch a passage:

```csharp
public sealed class PassageReader(IPassageClient passageClient)
{
    public async Task<string> GetJohn316Async(CancellationToken ct)
    {
        var passage = await passageClient.GetPassageAsync(3034, Reference.FromString("JHN.3.16"), cancellationToken: ct);
        return passage.Content;
    }
}
```

`Reference` (from `BiblePlatform.UsfmReferences`) normalizes and validates the USFM string before it's
sent to the API — there's no implicit conversion from a raw `string`.

See [`Platform.API/README.md`](../Platform.API/README.md#common-usage-examples) for more usage
examples, including books/chapters/verses structure and creating highlights, or
[`PlatformConsoleSample`](../PlatformConsoleSample/README.md) for a complete runnable version of
this guide's example.

## Next steps

- Need users to sign in (e.g. to write highlights)? See the [OAuth guide](oauth-guide.md).
- Building a Blazor app? Install `BiblePlatform.SDK.Components` instead and see
  [`Platform.SDK.Components/README.md`](../Platform.SDK.Components/README.md).
- Want a service layer between the raw client and your UI? See
  [`Platform.SDK.Services/README.md`](../Platform.SDK.Services/README.md).
