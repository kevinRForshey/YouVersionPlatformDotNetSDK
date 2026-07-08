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
    if (ctx.Request.Path == "/" && ctx.Request.Query.ContainsKey("code"))
    {
        ctx.Session.SetString("oauth_code", ctx.Request.Query["code"].ToString());
        ctx.Session.SetString("oauth_state_return", ctx.Request.Query["state"].ToString());
        ctx.Response.Redirect("/auth/callback-complete");
        return;
    }

    // Final leg of the Data Exchange consent flow (triggered from /auth/callback-complete after
    // sign-in). YouVersion redirects here with the outcome instead of returning a token — the
    // access token obtained during sign-in is what becomes authorized for the granted permission.
    if (ctx.Request.Path == "/" && ctx.Request.Query.ContainsKey("data_exchange_status"))
    {
        var granted = ctx.Request.Query["data_exchange_status"].ToString() == "granted";
        ctx.Response.Redirect($"/?auth_mode=code&highlights={(granted ? "granted" : "denied")}");
        return;
    }

    // Dev-only convenience: lets the UI be exercised without a live OAuth round trip.
    // Builds an unsigned token from raw query params, so it must never be reachable
    // outside Development — anyone could pass ?user_name=admin and appear signed in.
    if (app.Environment.IsDevelopment() && ctx.Request.Path == "/" &&
        (ctx.Request.Query.ContainsKey("user_name") || ctx.Request.Query.ContainsKey("user_email")))
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

// OAuth login redirect endpoint — writes PKCE verifier to session then redirects
// to the YouVersion authorization server. Must be a minimal API (not a Blazor page)
// so HttpContext.Session is writable before the external redirect occurs.
app.MapGet("/auth/login", (IYouVersionOAuthClient oauthClient, HttpContext ctx) =>
{
    var state = Base64Url(RandomNumberGenerator.GetBytes(16));
    var authRequest = oauthClient.BuildAuthorizationUrl(state);
    ctx.Session.SetString("pkce_verifier", authRequest.Pkce.CodeVerifier);
    ctx.Session.SetString("oauth_state", state);
    return Results.Redirect(authRequest.AuthorizationUrl.ToString());
});

app.MapGet("/auth/logout", async (IYouVersionOAuthClient oauthClient, HttpContext ctx) =>
{
    await oauthClient.SignOutAsync();
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

    // Sign-in succeeded. Basic sign-in only grants identity — chain into the Data Exchange
    // consent flow to additionally request highlights access. If this step fails, sign-in still
    // succeeds; the user simply won't have highlights access yet.
    try
    {
        var dataExchangeToken = await oauthClient.RequestPermissionsAsync(["highlights"]);
        var approvalUrl = oauthClient.BuildDataExchangeApprovalUrl(dataExchangeToken.Token);
        return Results.Redirect(approvalUrl.ToString());
    }
    catch (Exception ex)
    {
        ctx.RequestServices.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "Data exchange permission request failed.");
        return Results.Redirect("/?auth_mode=code");
    }
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

