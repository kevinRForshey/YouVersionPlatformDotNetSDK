# Unofficial-YouVersion.Platform.API

Part of the [YouVersion Platform SDK for .NET](../README.md).

Typed HTTP client SDK for the [YouVersion Platform REST API](https://developers.youversion.com).

Depends on [`Unofficial-YouVersion.Platform.API.Models`](../Platform.API.Models/README.md) for its
request/response types, and on [`Unofficial-YouVersion.UsfmReferences`](../YouVersion.UsfmReferences/README.md)
for parsing scripture references such as `Reference` and `VerseRange`.

## What this package provides

- `IBibleClient` for Bible discovery, version metadata, and book/chapter/verse structure.
- `IPassageClient` for passage text/HTML retrieval.
- `IHighlightClient` for highlight read/write operations (get per-passage, recent colors, create-or-update, clear).
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
  <PackageReference Include="Unofficial-YouVersion.Platform.API" Version="0.1.2" />
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

## Configuration & secrets

`AppKey`, `ClientId`, and any OAuth secret are read through standard `IConfiguration`/`IOptions<T>`
binding — the SDK doesn't care which provider they come from. The `appsettings.json` example above
is fine for shape/structure, but don't put real keys directly in a file that ships with your app or
gets committed to source control:

- **Local development**: use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
  instead of a real key in `appsettings.Development.json`:

  ```bash
  dotnet user-secrets init
  dotnet user-secrets set "YouVersionApi:AppKey" "YOUR_APP_KEY"
  dotnet user-secrets set "YouVersionOAuth:ClientId" "YOUR_CLIENT_ID"
  ```

  User Secrets are stored outside the project directory (keyed to a per-project id in
  `.csproj`), so they can't be accidentally committed even without a `.gitignore` entry.

- **Production**: supply values via environment variables (ASP.NET Core maps `__` to `:`, so
  `YouVersionApi:AppKey` becomes `YouVersionApi__AppKey`) or a secret manager — Azure Key Vault,
  AWS Secrets Manager, etc. — registered as an additional `IConfiguration` source. Never check a
  live key into any `appsettings*.json` file.

Since each consuming application supplies its own key/client id through its own configuration
sources, no code in this SDK needs to change between environments — only where `builder.Configuration`
is told to look.

## OAuth setup (optional)

Use this only when you need user-scoped operations (for example, highlight writes). YouVersion's
sign-in API only supports the `openid`, `profile`, and `email` scopes — there is no separate scope
for passages or highlights; any authenticated user can call those endpoints.

```csharp
builder.Services
    .AddYouVersionApiClients(builder.Configuration)
    .AddYouVersionOAuth(options =>
    {
        options.ClientId = builder.Configuration["YouVersionOAuth:ClientId"]!;
        options.RedirectUri = new Uri(builder.Configuration["YouVersionOAuth:RedirectUri"]!);
        options.Scopes = "openid profile email";
    });
```

> `AddYouVersionApiClients(...)` must be called before `AddYouVersionOAuth(...)`.

## Data Exchange (resource permissions)

Signing in only grants identity. To let a user grant your app access to a protected resource
(currently `highlights`), they need to go through the **Data Exchange approval page** — a
separate, explicit consent step. Once approved, the same access token from sign-in becomes
authorized for that resource; no new token is issued. There are two ways to trigger this,
depending on when you need consent:

### Requesting permissions during sign-in (recommended)

Pass `requestedPermissions` to `BuildAuthorizationUrl`. YouVersion shows the consent UI as part
of the same sign-in redirect — no extra round trip. In practice the grant result has been observed
arriving via **either** of two shapes, and your callback handler should be prepared for both:

- Folded into the same callback that carries `code`/`state`, as a `granted_permissions` query
  parameter (comma-separated; empty string if requested but denied; omitted if nothing was
  requested); or
- As a **separate** follow-up hit on your `RedirectUri` carrying `data_exchange_status` — the same
  shape the standalone flow below uses. Call `ParseDataExchangeCallback` to handle it.

```csharp
var authRequest = oauthClient.BuildAuthorizationUrl(state, requestedPermissions: ["highlights"]);
return Results.Redirect(authRequest.AuthorizationUrl.AbsoluteUri);

// On your configured RedirectUri, alongside code/state (first shape):
var granted = ctx.Request.Query["granted_permissions"].ToString()
    .Split(',', StringSplitOptions.RemoveEmptyEntries);
var highlightsGranted = granted.Contains("highlights", StringComparer.OrdinalIgnoreCase);

// Or, if it instead arrives as a separate callback (second shape):
var result = oauthClient.ParseDataExchangeCallback(new Uri(ctx.Request.GetEncodedUrl()));
```

See `PlatformTestApp/Program.cs` for a complete working example that handles both shapes.

### Requesting permissions after the user is already signed in

Use this when you need to ask for a resource permission later, without sending the user through
a full sign-in round trip again. The flow has three parts:

1. **Request a short-lived Data Exchange token** — `RequestPermissionsAsync` calls
   `POST /data-exchange/token` with the signed-in user's access token and returns a
   `DataExchangeToken` (opaque, single-use, expires in ~5 minutes).
2. **Redirect the user to the approval page** — `BuildDataExchangeApprovalUrl` builds the
   `GET /data-exchange` URL. Because this is a top-level browser redirect, it can't carry the
   `X-YVP-App-Key` header, so the app key is included as an `x-yvp-app-key` query parameter
   instead (sourced from `YouVersionApiOptions.AppKey`).
3. **Handle the callback** — YouVersion redirects the browser back to your configured
   `RedirectUri` with the outcome in the query string. Call `ParseDataExchangeCallback` to turn
   that into a typed `DataExchangeCallbackResult` (`Status`, `GrantedPermissions`,
   `DeniedPermissions`, `Error`, `ErrorDescription`) instead of hand-parsing query parameters.

```csharp
// After ExchangeCodeAsync succeeds during sign-in:
var dataExchangeToken = await oauthClient.RequestPermissionsAsync(["highlights"]);
var approvalUrl = oauthClient.BuildDataExchangeApprovalUrl(dataExchangeToken.Token);
return Results.Redirect(approvalUrl.AbsoluteUri);

// Later, on your configured RedirectUri:
var result = oauthClient.ParseDataExchangeCallback(new Uri(ctx.Request.GetEncodedUrl()));
switch (result.Status)
{
    case DataExchangeStatus.Granted:
        // result.GrantedPermissions contains "highlights"
        break;
    case DataExchangeStatus.Cancelled:
        // the user declined; result.DeniedPermissions / result.Error explain why
        break;
    case DataExchangeStatus.Error:
        // a recoverable error occurred; see result.Error / result.ErrorDescription
        break;
}
```

### Completing approval without the browser page

A confidential client that already has another basis for the user's consent can skip the
browser redirect entirely and complete approval directly, using the token from step 1:

```csharp
var dataExchangeToken = await oauthClient.RequestPermissionsAsync(["highlights"]);
var result = await oauthClient.CompleteDataExchangeApprovalAsync(dataExchangeToken.Token);
```

This calls `POST /data-exchange?token={token}` and parses the resulting `303` redirect's
`Location` header with the same `DataExchangeCallbackResult` used by the browser flow. The
permissions being granted are the ones fixed when the token was created — `POST /data-exchange`
takes no request body, so they can't (and don't need to) be sent again here.

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

### Create or update a highlight (OAuth required)

```csharp
public sealed class HighlightWriter(IHighlightClient highlightClient)
{
    public Task<Highlight> HighlightVerseAsync(CancellationToken ct)
        => highlightClient.CreateOrUpdateHighlightAsync(3034, Reference.FromString("JHN.3.16"), "44aa44", ct);
}
```

Highlights have no opaque id — a highlight is identified by `(bibleId, passageId)`. Creating a
highlight for a passage that already has one updates its color. Colors are hex strings (e.g.
`"44aa44"`), not a fixed enum. `ClearHighlightsAsync(bibleId, passage, ct)` removes a passage's
highlight(s), and `GetRecentColorsAsync(ct)` returns the colors the user has most recently used.

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

- [Getting started](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK/blob/main/docs/getting-started.md)
- [Authentication (app key)](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK/blob/main/docs/authentication.md)
- [OAuth guide](https://github.com/kevinRForshey/YouVersionPlatformDotNetSDK/blob/main/docs/oauth-guide.md)

## Related packages

- [`Unofficial-YouVersion.Platform.API.Models`](../Platform.API.Models/README.md) — the model types this client returns.
- [`Unofficial-YouVersion.Platform.SDK.Services`](../Platform.SDK.Services/README.md) — business-logic services built on top of this client; consider this layer instead of calling `Platform.API` directly when building your own UI.
- [`Unofficial-YouVersion.Platform.SDK.Components`](../Platform.SDK.Components/README.md) — ready-made Blazor UI that consumes this client transitively.

## Build and pack

```powershell
dotnet pack -c Release
```
