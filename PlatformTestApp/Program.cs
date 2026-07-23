using Microsoft.FluentUI.AspNetCore.Components;

using Platform.API.Extensions;
using Platform.API.OAuth;
using Platform.SDK.Components.Extensions;

using PlatformTestApp.Auth;
using PlatformTestApp.Components;

using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
#region Add Services
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();

builder.Services.AddBibleApiClients(builder.Configuration);
builder.Services.AddBibleCaching(o =>
    builder.Configuration.GetSection(Platform.API.Configuration.BibleCacheOptions.SectionName).Bind(o));

// Must be registered before AddBibleOAuth: the library only adds its default
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

builder.Services.AddBibleOAuth(o =>
{
    builder.Configuration.GetSection("BibleOAuth").Bind(o);

    // Apps can use the app key as the OAuth client identifier.
    if (string.IsNullOrWhiteSpace(o.ClientId))
        o.ClientId = builder.Configuration["BibleApi:AppKey"] ?? string.Empty;
});

builder.Services.AddBibleComponents();

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
// The platform redirects back to http://localhost:52413?code=...&state=...
// Each callback shape is handled by its own method in OAuthCallbackHandlers; this stays a flat
// dispatch list so a new callback shape is one more line here, not one more inline `if` block.
app.Use(async (HttpContext ctx, RequestDelegate next) =>
{
    if (ctx.Request.Path != "/" || ctx.Request.Query.Count == 0)
    {
        await next(ctx);
        return;
    }

    OAuthCallbackHandlers.LogIncomingQuery(ctx);

    if (OAuthCallbackHandlers.TryHandleAuthorizationCodeCallback(ctx))
        return;

    if (await OAuthCallbackHandlers.TryHandleDataExchangeCallbackAsync(ctx))
        return;

    if (await OAuthCallbackHandlers.TryHandleIdentityCallbackAsync(ctx))
        return;

    if (await OAuthCallbackHandlers.TryHandleDevSignInAsync(ctx, app.Environment.IsDevelopment()))
        return;

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
static IResult RedirectToAuthorize(IBibleOAuthClient oauthClient, HttpContext ctx, IEnumerable<string>? requestedPermissions)
{
    var state = Base64Url(RandomNumberGenerator.GetBytes(16));
    var authRequest = oauthClient.BuildAuthorizationUrl(state, requestedPermissions);
    ctx.Session.SetString("pkce_verifier", authRequest.Pkce.CodeVerifier);
    ctx.Session.SetString("oauth_state", state);
    return Results.Redirect(authRequest.AuthorizationUrl.AbsoluteUri);
}

// OAuth login redirect endpoint — writes PKCE verifier to session then redirects
// to the platform's authorization server. Must be a minimal API (not a Blazor page)
// so HttpContext.Session is writable before the external redirect occurs.
app.MapGet("/auth/login", (IBibleOAuthClient oauthClient, HttpContext ctx) =>
    RedirectToAuthorize(oauthClient, ctx, requestedPermissions: null));

// "Grant highlights access" for an already-signed-in user — same redirect as /auth/login, but
// requesting "highlights". See RedirectToAuthorize for why this path is used instead of the
// separate RequestPermissionsAsync + BuildDataExchangeApprovalUrl round trip.
app.MapGet("/auth/request-highlights", (IBibleOAuthClient oauthClient, HttpContext ctx) =>
    RedirectToAuthorize(oauthClient, ctx, requestedPermissions: ["highlights"]));

app.MapGet("/auth/logout", async (IBibleOAuthClient oauthClient, HttpContext ctx) =>
{
    await oauthClient.SignOutAsync();
    await ctx.RequestServices.GetRequiredService<HighlightsPermissionStore>().SetGrantedAsync(false);
    ctx.Session.Clear();
    return Results.Redirect("/");
});

// Completes the OAuth code exchange. Called by the middleware after parking the code in session.
// Minimal API endpoints have a real HttpContext and reliable session access — Blazor components don't.
app.MapGet("/auth/callback-complete", async (IBibleOAuthClient oauthClient, HttpContext ctx) =>
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

