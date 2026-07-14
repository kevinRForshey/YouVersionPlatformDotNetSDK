using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;

using Platform.API.Extensions;
using Platform.API.OAuth;
using Platform.SDK.Components.Extensions;

using PlatformTestApp.Auth;
using PlatformTestApp.Components;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
#region Add Services
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();

builder.Services.AddYouVersionApiClients(builder.Configuration);
builder.Services.AddYouVersionCaching(o =>
    builder.Configuration.GetSection(Platform.API.Configuration.YouVersionCacheOptions.SectionName).Bind(o));

// Must be registered before AddYouVersionOAuth: the library only adds its default
// InMemoryTokenProvider via TryAddSingleton, which is a per-process singleton shared
// by every user. Registering a per-session provider first makes TryAddSingleton a no-op.
//
// CircuitSessionKeyAccessor + IDistributedCache (rather than ISession directly) so the token
// stays readable for the full lifetime of an interactive Blazor Server circuit, not just during
// the HTTP request that stored it — HttpContext/ISession are only live during that request.
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<CircuitSessionKeyAccessor>();
builder.Services.AddScoped<ITokenProvider, SessionTokenProvider>();
builder.Services.AddScoped<HighlightsPermissionStore>();

builder.Services.AddYouVersionOAuth(o =>
{
    builder.Configuration.GetSection("YouVersionOAuth").Bind(o);

    // YouVersion apps can use the app key as the OAuth client identifier.
    if (string.IsNullOrWhiteSpace(o.ClientId))
        o.ClientId = builder.Configuration["YouVersionApi:AppKey"] ?? string.Empty;
});

builder.Services.AddYouVersionComponents();

// Session support for OAuth PKCE code verifier / state storage
builder.Services.AddSession(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(15);
});
#endregion
var app = builder.Build();

#region HTTP Pipeline
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseSession();

#endregion
// YouVersion redirects back to http://localhost:52413?code=...&state=...
// Park the code in the session then hand off to the callback endpoint which has a real HttpContext.
app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    if (ctx.Request.Path == "/" && ctx.Request.Query.Count > 0)
    {
        // Every callback shape lands here first; log so an unhandled shape (e.g. ?error=...)
        // is visible instead of silently falling through to the home page.
        ctx.RequestServices.GetRequiredService<ILogger<Program>>()
            .LogDebug("Landed on \"/\" with query: {Query}", ctx.Request.QueryString);
    }

    if (ctx.Request.Path == "/" && ctx.Request.Query.ContainsKey("code"))
    {
        ctx.Session.SetString("oauth_code", ctx.Request.Query["code"].ToString());
        ctx.Session.SetString("oauth_state_return", ctx.Request.Query["state"].ToString());
        // Comma-separated grant result, present only when YouVersion folds it into this callback
        // instead of sending a separate data_exchange_status redirect (both shapes occur live).
        // Empty string = requested but denied; absent = not requested.
        if (ctx.Request.Query.ContainsKey("granted_permissions"))
            ctx.Session.SetString("oauth_granted_permissions", ctx.Request.Query["granted_permissions"].ToString());
        ctx.Response.Redirect("/auth/callback-complete");
        return;
    }

    // Standalone Data Exchange callback shape: consent result arrives as a separate hit on "/"
    // rather than folded into the code callback above (see ParseDataExchangeCallback). Confirmed
    // reachable via live sign-in — don't remove without re-verifying against a real browser flow.
    if (ctx.Request.Path == "/" && ctx.Request.Query.ContainsKey("data_exchange_status"))
    {
        var oauthClient = ctx.RequestServices.GetRequiredService<IYouVersionOAuthClient>();
        var result = oauthClient.ParseDataExchangeCallback(new Uri(ctx.Request.GetEncodedUrl()));

        var granted = result.Status == DataExchangeStatus.Granted;
        await ctx.RequestServices.GetRequiredService<HighlightsPermissionStore>().SetGrantedAsync(granted);

        var redirect = $"/?auth_mode=code&highlights={(granted ? "granted" : "denied")}";
        if (result.Status == DataExchangeStatus.Error)
            redirect += $"&oauth_error={Uri.EscapeDataString(result.ErrorDescription ?? result.Error ?? "Data exchange approval failed.")}";

        ctx.Response.Redirect(redirect);
        return;
    }

    // Browser clients get identity fields back, not a redeemable `code`:
    //   ?profile_picture=...&state=...&user_email=...&user_name=...&yvp_id=...&granted_permissions=highlights
    // Normal step-1 behavior (https://developers.youversion.com/sign-in-apis), not a fallback.
    // `yvp_id` only appears on a genuine YouVersion redirect, so it can't collide with the
    // dev-only shortcut below. CompleteIdentityCallbackAsync runs steps 2-3 and returns a real token.
    if (ctx.Request.Path == "/" && ctx.Request.Query.ContainsKey("yvp_id"))
    {
        var expectedState = ctx.Session.GetString("oauth_state");
        var returnedState = ctx.Request.Query["state"].ToString();
        var oauthClient = ctx.RequestServices.GetRequiredService<IYouVersionOAuthClient>();
        if (!oauthClient.ValidateState(expectedState, returnedState))
        {
            ctx.Response.Redirect($"/?oauth_error={Uri.EscapeDataString("State mismatch — possible CSRF attempt. Please try signing in again.")}&auth_mode=direct");
            return;
        }

        var verifier = ctx.Session.GetString("pkce_verifier");
        ctx.Session.Remove("oauth_state");
        ctx.Session.Remove("pkce_verifier");

        if (string.IsNullOrEmpty(verifier))
        {
            ctx.Response.Redirect($"/?oauth_error={Uri.EscapeDataString("Session expired mid sign-in. Please try again.")}");
            return;
        }

        var userName = ctx.Request.Query["user_name"].ToString();
        var userEmail = ctx.Request.Query["user_email"].ToString();
        var profilePicture = ctx.Request.Query["profile_picture"].ToString();
        var yvpId = ctx.Request.Query["yvp_id"].ToString();

        try
        {
            await oauthClient.CompleteIdentityCallbackAsync(
                returnedState, yvpId, userName, userEmail, profilePicture, verifier);
        }
        catch (Exception ex)
        {
            ctx.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogError(ex, "Completing the identity callback failed.");
            ctx.Response.Redirect($"/?oauth_error={Uri.EscapeDataString("Sign-in failed while completing the callback. Please try again.")}");
            return;
        }

        // Absent = not requested this round trip, NOT denied — treating it as denied would wipe
        // out an existing grant from a prior /auth/request-highlights approval.
        if (ctx.Request.Query.ContainsKey("granted_permissions"))
        {
            var highlightsGranted = ctx.Request.Query["granted_permissions"].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Contains("highlights", StringComparer.OrdinalIgnoreCase);
            await ctx.RequestServices.GetRequiredService<HighlightsPermissionStore>().SetGrantedAsync(highlightsGranted);
            ctx.Response.Redirect($"/?auth_mode=direct&highlights={(highlightsGranted ? "granted" : "denied")}");
            return;
        }

        ctx.Response.Redirect("/?auth_mode=direct");
        return;
    }

    // Dev-only shortcut to exercise the UI without a live OAuth round trip. Builds an unsigned
    // token from raw query params — must never be reachable outside Development, since anyone
    // could pass ?dev_signin=1&user_name=admin and appear signed in. Gated on the `dev_signin`
    // marker so it can't collide with a real callback, which is keyed on `yvp_id` instead.
    if (app.Environment.IsDevelopment() && ctx.Request.Path == "/" &&
        ctx.Request.Query.ContainsKey("dev_signin"))
    {
        var userName = ctx.Request.Query["user_name"].ToString();
        var userEmail = ctx.Request.Query["user_email"].ToString();

        var tokenProvider = ctx.RequestServices.GetRequiredService<ITokenProvider>();
        var claims = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(userName))
            claims["name"] = userName;
        if (!string.IsNullOrWhiteSpace(userEmail))
            claims["email"] = userEmail;

        var syntheticIdToken = BuildUnsignedJwt(claims);
        await tokenProvider.StoreTokenAsync(new OAuthTokenResponse
        {
            AccessToken = "oauth-session-user",
            IdToken = syntheticIdToken,
            ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow
        });

        ctx.Response.Redirect("/?auth_mode=direct");
        return;
    }

    await next(ctx);
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Builds a fresh PKCE authorization redirect and parks the verifier/state in session. Shared by
// /auth/login and /auth/request-highlights so both land on the same yvp_id callback branch and
// reuse the same tested sign-in path, rather than /auth/request-highlights taking the separate
// (less-exercised) RequestPermissionsAsync + BuildDataExchangeApprovalUrl round trip. Switch to
// that two-step flow if avoiding the extra redirect for already-signed-in users matters more.
static IResult RedirectToAuthorize(IYouVersionOAuthClient oauthClient, HttpContext ctx, IEnumerable<string>? requestedPermissions)
{
    var state = Base64Url(RandomNumberGenerator.GetBytes(16));
    var authRequest = oauthClient.BuildAuthorizationUrl(state, requestedPermissions);
    ctx.Session.SetString("pkce_verifier", authRequest.Pkce.CodeVerifier);
    ctx.Session.SetString("oauth_state", state);
    return Results.Redirect(authRequest.AuthorizationUrl.AbsoluteUri);
}

// OAuth login redirect endpoint — writes PKCE verifier to session then redirects
// to the YouVersion authorization server. Must be a minimal API (not a Blazor page)
// so HttpContext.Session is writable before the external redirect occurs.
app.MapGet("/auth/login", (IYouVersionOAuthClient oauthClient, HttpContext ctx) =>
    RedirectToAuthorize(oauthClient, ctx, requestedPermissions: null));

// "Grant highlights access" for an already-signed-in user — same redirect as /auth/login, but
// requesting "highlights". See RedirectToAuthorize for why this path is used instead of the
// separate RequestPermissionsAsync + BuildDataExchangeApprovalUrl round trip.
app.MapGet("/auth/request-highlights", (IYouVersionOAuthClient oauthClient, HttpContext ctx) =>
    RedirectToAuthorize(oauthClient, ctx, requestedPermissions: ["highlights"]));

app.MapGet("/auth/logout", async (IYouVersionOAuthClient oauthClient, HttpContext ctx) =>
{
    await oauthClient.SignOutAsync();
    await ctx.RequestServices.GetRequiredService<HighlightsPermissionStore>().SetGrantedAsync(false);
    ctx.Session.Clear();
    return Results.Redirect("/");
});

// Completes the OAuth code exchange. Called by the middleware after parking the code in session.
// Minimal API endpoints have a real HttpContext and reliable session access — Blazor components don't.
app.MapGet("/auth/callback-complete", async (IYouVersionOAuthClient oauthClient, HttpContext ctx) =>
{
    var code = ctx.Session.GetString("oauth_code");
    var retState = ctx.Session.GetString("oauth_state_return");
    var verifier = ctx.Session.GetString("pkce_verifier");
    var expected = ctx.Session.GetString("oauth_state");

    ctx.Session.Remove("oauth_code");
    ctx.Session.Remove("oauth_state_return");
    ctx.Session.Remove("pkce_verifier");
    ctx.Session.Remove("oauth_state");

    string? exchangeError = null;

    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(verifier))
    {
        exchangeError = "Session expired or invalid callback. Please try signing in again.";
    }
    else if (!oauthClient.ValidateState(expected, retState))
    {
        exchangeError = "State mismatch — possible CSRF attempt. Please try signing in again.";
    }
    else
    {
        try
        {
            await oauthClient.ExchangeCodeAsync(code, verifier);
        }
        catch (Exception ex)
        {
            ctx.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogError(ex, "OAuth code exchange failed.");
            exchangeError = "Sign-in failed. Please try again.";
        }
    }

    if (exchangeError is not null)
        return Results.Redirect($"/?oauth_error={Uri.EscapeDataString(exchangeError)}&auth_mode=code");

    // Defensive fallback only — /auth/login no longer requests permissions inline, so this should
    // rarely be present. Absent means "not requested," NOT "denied": treating it as denied would
    // overwrite an existing grant from a prior /auth/request-highlights approval.
    var grantedPermissions = ctx.Session.GetString("oauth_granted_permissions");
    ctx.Session.Remove("oauth_granted_permissions");

    var redirect = "/?auth_mode=code";
    if (grantedPermissions is not null)
    {
        var highlightsGranted = grantedPermissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Contains("highlights", StringComparer.OrdinalIgnoreCase);
        await ctx.RequestServices.GetRequiredService<HighlightsPermissionStore>().SetGrantedAsync(highlightsGranted);
        redirect += $"&highlights={(highlightsGranted ? "granted" : "denied")}";
    }

    return Results.Redirect(redirect);
});

app.Run();

static string Base64Url(byte[] bytes) =>
    Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

static string BuildUnsignedJwt(IReadOnlyDictionary<string, string> claims)
{
    var payloadJson = JsonSerializer.Serialize(claims);
    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    return $"header.{payload}.signature";
}

