# Authentication (app key)

The Platform API has two independent layers of authentication, and it's easy to conflate them:

| Layer | Proves | Required for |
|---|---|---|
| **App key** (this doc) | Which *application* is calling the API | Every request — even read-only ones like listing versions or fetching a passage |
| **OAuth/PKCE** (see the [OAuth guide](oauth-guide.md)) | Which *user* is signed in | User-scoped operations, e.g. writing a highlight |

This doc covers the app key only. If you also need user sign-in, read this first, then move on to
the [OAuth guide](oauth-guide.md).

## What the app key is

A per-application credential you obtain by registering at
[developers.youversion.com](https://developers.youversion.com). The SDK sends it as the
`X-YVP-App-Key` header on every outgoing request via `AppKeyDelegatingHandler`
([`Platform.API/Http/AppKeyDelegatingHandler.cs`](../Platform.API/Http/AppKeyDelegatingHandler.cs)) —
you never construct or attach that header yourself.

If `YouVersionApiOptions.AppKey` is unset when a call is made, the handler throws an
`InvalidOperationException` naming the missing option rather than sending an unauthenticated
request.

## Configuring it

```csharp
builder.Services.AddYouVersionApiClients(options =>
{
    options.AppKey = builder.Configuration["YouVersionApi:AppKey"]!;
});
```

`AppKey`, and any OAuth client id/secret, are read through standard
`IConfiguration`/`IOptions<T>` binding — the SDK doesn't care which provider they come from. An
`appsettings.json` entry is fine for shape/structure during development, but don't put a real key
directly in a file that ships with your app or gets committed to source control:

- **Local development** — use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
  instead of a real key in `appsettings.Development.json`:

  ```bash
  dotnet user-secrets init
  dotnet user-secrets set "YouVersionApi:AppKey" "YOUR_APP_KEY"
  dotnet user-secrets set "YouVersionOAuth:ClientId" "YOUR_CLIENT_ID"
  ```

  User Secrets are stored outside the project directory (keyed to a per-project id in the
  `.csproj`), so they can't be accidentally committed even without a `.gitignore` entry.

- **Production** — supply values via environment variables (ASP.NET Core maps `__` to `:`, so
  `YouVersionApi:AppKey` becomes `YouVersionApi__AppKey`) or a secret manager — Azure Key Vault, AWS
  Secrets Manager, etc. — registered as an additional `IConfiguration` source. Never check a live
  key into any `appsettings*.json` file.

Since each consuming application supplies its own key/client id through its own configuration
sources, no code in this SDK needs to change between environments — only where
`builder.Configuration` is told to look.

## Troubleshooting

- `InvalidOperationException` mentioning `YouVersionApiOptions.AppKey`: the app key wasn't set
  before the first API call — check the configuration section name (`YouVersionApi`) and binding
  path match what you passed to `AddYouVersionApiClients`.
- `YouVersionApiException` with `401`/`403`: the app key is set but invalid, revoked, or not
  authorized for the endpoint being called.

## Next steps

Once the app key is working, see [Getting started](getting-started.md) for your first API calls,
or the [OAuth guide](oauth-guide.md) if you also need users to sign in.
