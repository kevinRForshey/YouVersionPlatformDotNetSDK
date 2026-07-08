# YouVersion.Platform.API

Typed HTTP client SDK for the [YouVersion Platform REST API](https://developers.youversion.com).

## What this package provides

- `IBibleClient` for Bible discovery, version metadata, and book/chapter/verse structure.
- `IPassageClient` for passage text/HTML retrieval.
- `IHighlightClient` for highlight read/write operations. (not fully implemented)
- `IYouVersionOAuthClient` for authorization-code + PKCE OAuth flow.
- `ITokenProvider` (default: `InMemoryTokenProvider`) for token persistence.
- Built-in `HttpClient` resilience and outbound rate limiting.
- DI extensions: `AddYouVersionApiClients(...)` and `AddYouVersionOAuth(...)`.

## Target framework

- `net10.0`

## Installation

Add package references as needed:

```xml
<ItemGroup>
  <PackageReference Include="YouVersion.Platform.API" Version="1.0.0" />
</ItemGroup>
```

## Minimal setup (no sign-in required)

For read-only operations (versions, books, passages), only an app key is required.

```csharp
builder.Services.AddYouVersionApiClients(options =>
{
    options.AppKey = builder.Configuration["YouVersionApi:AppKey"]!;
});
```

Example `appsettings.json`:

```json
{
  "YouVersionApi": {
    "AppKey": "YOUR_APP_KEY",
    "BaseAddress": "https://api.youversion.com",
    "Timeout": "00:00:30",
    "OutboundRequestsPerSecond": 10,
    "OutboundBurstSize": 20,
    "OutboundQueueLimit": 100
  }
}
```

## OAuth setup (optional)

Use this only when you need user-scoped operations (for example, highlight writes).

```csharp
builder.Services
    .AddYouVersionApiClients(builder.Configuration)
    .AddYouVersionOAuth(options =>
    {
        options.ClientId = builder.Configuration["YouVersionOAuth:ClientId"]!;
        options.RedirectUri = new Uri(builder.Configuration["YouVersionOAuth:RedirectUri"]!);
        options.Scopes = "passages highlights";
    });
```

> `AddYouVersionApiClients(...)` must be called before `AddYouVersionOAuth(...)`.

## Common usage examples

### List available Bible versions

```csharp
public sealed class VersionReader(IBibleClient bibleClient)
{
    public Task<PagedResult<BibleVersionSummary>> GetEnglishVersionsAsync(CancellationToken ct)
        => bibleClient.GetVersionsAsync(languageRange: "en", cancellationToken: ct);
}
```

### Get books, chapters, and verses for a version

`GetBooksAsync`, `GetChaptersAsync`, and `GetVersesAsync` are all backed by a single cached call
to `GET /v1/bibles/{id}/index`, so the returned structure (chapter counts per book, verse counts
per chapter) always reflects the actual content of that specific version rather than a generic
guess. Verses returned this way carry no scripture text — use `IPassageClient` for that.

```csharp
public sealed class BibleStructureReader(IBibleClient bibleClient)
{
    public Task<IReadOnlyList<Book>> GetBooksAsync(CancellationToken ct)
        => bibleClient.GetBooksAsync(versionId: 3034, cancellationToken: ct);

    public Task<IReadOnlyList<Chapter>> GetChaptersAsync(CancellationToken ct)
        => bibleClient.GetChaptersAsync(versionId: 3034, bookUsfm: "GEN", cancellationToken: ct);

    public Task<IReadOnlyList<Verse>> GetVersesAsync(CancellationToken ct)
        => bibleClient.GetVersesAsync(versionId: 3034, bookUsfm: "GEN", chapterNumber: 1, cancellationToken: ct);
}
```

You can also fetch the raw index directly if you need the full nested structure (including
canon and intro sections) in one call:

```csharp
BibleIndex index = await bibleClient.GetIndexAsync(versionId: 3034, cancellationToken: ct);
```

### Fetch passage text

```csharp
public sealed class PassageReader(IPassageClient passageClient)
{
    public async Task<string> GetJohn316Async(CancellationToken ct)
    {
        var passage = await passageClient.GetPassageAsync(3034, "JHN.3.16", cancellationToken: ct);
        return passage.Content;
    }
}
```

### Create a highlight (OAuth required)

```csharp
public sealed class HighlightWriter(IHighlightClient highlightClient)
{
    public Task<Highlight> HighlightVerseAsync(CancellationToken ct)
        => highlightClient.CreateHighlightAsync(3034, "JHN.3.16", HighlightColor.Yellow, ct);
}
```

## Resilience and outbound rate limiting

The SDK configures `HttpClient` with:

- standard resilience handler (retry/backoff/timeout strategy), and
- local outbound token-bucket rate limiting per typed client.

Rate limit knobs (`YouVersionApi` options):

- `OutboundRequestsPerSecond`: refill rate.
- `OutboundBurstSize`: maximum immediate burst.
- `OutboundQueueLimit`: waiting request queue size.

Suggested starting point for read-heavy apps:

- `OutboundRequestsPerSecond = 10`
- `OutboundBurstSize = 20`
- `OutboundQueueLimit = 100`

If you see local throttling, increase burst and/or queue gradually. If upstream throttling (`429`) appears, lower request rate.

## Token storage

Default OAuth token storage (`InMemoryTokenProvider`) is a **process-wide singleton** — fine for
single-user tools and local testing, but in a multi-user host (e.g. Blazor Server) it leaks one
user's token to every other user on the same process. For any app with more than one concurrent
user, register a custom, per-user-scoped `ITokenProvider` before `AddYouVersionOAuth(...)`:

```csharp
builder.Services.AddScoped<ITokenProvider, MyPerUserTokenProvider>();
```

For a working example that scopes tokens per browser session using `IDistributedCache`, see
`PlatformTestApp/Auth/SessionTokenProvider.cs` and `CircuitSessionKeyAccessor.cs` in this
solution's test app — it keys stored tokens off a session id that survives the handoff from
Blazor Server prerender into the live interactive circuit.

## Exceptions

- `YouVersionApiException` — the API returned a non-success HTTP response. Carries `StatusCode` and the raw `ResponseBody`.
- `YouVersionEmptyResponseException` (derives from `YouVersionApiException`) — the HTTP call itself succeeded (`200 OK`), but the body was null, empty, or failed to deserialize into the expected type. Check for this type first (or catch it separately) rather than branching on `StatusCode`, since it doesn't carry a real wire-level error status.

## Troubleshooting

- `InvalidOperationException` mentioning `AddYouVersionApiClients`: call order is wrong; register API clients first.
- `YouVersionApiException` with `401`/`403`: check app key and (for write ops) OAuth token state.
- `YouVersionEmptyResponseException`: the request succeeded but returned no usable body — usually a transient upstream issue; safe to retry.
- Local outbound throttling errors: tune `OutboundRequestsPerSecond`, `OutboundBurstSize`, and `OutboundQueueLimit`.

## Additional docs

- [Getting started](../docs/getting-started.md)
- [Authentication (app key)](../docs/authentication.md)
- [OAuth guide](../docs/oauth-guide.md)

## Build and pack

```powershell
dotnet pack -c Release
```
