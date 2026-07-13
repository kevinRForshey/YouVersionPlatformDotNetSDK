using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;
using Platform.API.Exceptions;
using Platform.API.OAuth;
using Platform.API.Tests.Fakes;
using Xunit;

namespace Platform.API.Tests.OAuth;

public sealed class OAuthClientTests
{
    private const string TokenJson = """
        { "access_token": "acc-tok", "refresh_token": "ref-tok",
          "token_type": "Bearer", "expires_in": 3600 }
        """;

    // -------------------------------------------------------------------------
    // ValidateState
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateState_ReturnsTrue_WhenValuesMatch()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        client.ValidateState("csrf-token-abc", "csrf-token-abc").Should().BeTrue();
    }

    [Fact]
    public void ValidateState_ReturnsFalse_WhenValuesDiffer()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        client.ValidateState("csrf-token-abc", "csrf-token-xyz").Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "state")]
    [InlineData("state", null)]
    [InlineData("", "state")]
    [InlineData("state", "")]
    [InlineData(null, null)]
    public void ValidateState_ReturnsFalse_WhenEitherValueIsMissing(string? expected, string? actual)
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        client.ValidateState(expected, actual).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // BuildAuthorizationUrl
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildAuthorizationUrl_ReturnsUri_WithExpectedQueryParameters()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl();

        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("response_type=code");
        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("client_id=test-client");
        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("code_challenge_method%3DS256"   // encoded
            .Replace("%3D", "="));
        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("redirect_uri=");
    }

    [Fact]
    public void BuildAuthorizationUrl_GeneratesPkce_WithNonEmptyVerifierAndChallenge()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl();

        authRequest.Pkce.CodeVerifier.Should().NotBeNullOrEmpty();
        authRequest.Pkce.CodeChallenge.Should().NotBeNullOrEmpty();
        authRequest.Pkce.CodeChallengeMethod.Should().Be("S256");
    }

    [Fact]
    public void BuildAuthorizationUrl_EachCallProducesUniquePkce()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var req1 = client.BuildAuthorizationUrl();
        var req2 = client.BuildAuthorizationUrl();

        req1.Pkce.CodeVerifier.Should().NotBe(req2.Pkce.CodeVerifier);
        req1.Pkce.CodeChallenge.Should().NotBe(req2.Pkce.CodeChallenge);
    }

    [Fact]
    public void BuildAuthorizationUrl_UsesProvidedState_InQueryString()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl(state: "csrf-token-123");

        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("state=csrf-token-123");
    }

    [Fact]
    public void BuildAuthorizationUrl_GeneratesState_WhenNotProvided()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl();

        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("state=");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildAuthorizationUrl_ThrowsArgumentException_WhenProvidedStateIsInvalid(string state)
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);
        var act = () => client.BuildAuthorizationUrl(state);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildAuthorizationUrl_OmitsRequestedPermissions_WhenNotProvided()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl();

        authRequest.AuthorizationUrl.AbsoluteUri.Should().NotContain("requested_permissions");
    }

    [Fact]
    public void BuildAuthorizationUrl_AppendsRequestedPermissions_WhenProvided()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl(requestedPermissions: ["highlights"]);

        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("requested_permissions=highlights");
    }

    [Fact]
    public void BuildAuthorizationUrl_AppendsEachRequestedPermission_AsRepeatedQueryParameter()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var authRequest = client.BuildAuthorizationUrl(requestedPermissions: ["highlights", "other"]);

        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("requested_permissions=highlights");
        authRequest.AuthorizationUrl.AbsoluteUri.Should().Contain("requested_permissions=other");
    }

    // -------------------------------------------------------------------------
    // ExchangeCodeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExchangeCodeAsync_ReturnsToken_WhenApiSucceeds()
    {
        var tokenProvider = new FakeTokenProvider();
        var client = BuildClient(HttpStatusCode.OK, TokenJson, tokenProvider);

        var token = await client.ExchangeCodeAsync("auth-code", "verifier");

        token.AccessToken.Should().Be("acc-tok");
        token.RefreshToken.Should().Be("ref-tok");
        token.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task ExchangeCodeAsync_StoresTokenInProvider_AfterSuccess()
    {
        var tokenProvider = new FakeTokenProvider();
        var client = BuildClient(HttpStatusCode.OK, TokenJson, tokenProvider);

        await client.ExchangeCodeAsync("auth-code", "verifier");

        var stored = await tokenProvider.GetTokenAsync();
        stored.Should().NotBeNull();
        stored!.AccessToken.Should().Be("acc-tok");
    }

    [Fact]
    public async Task ExchangeCodeAsync_ThrowsYouVersionApiException_WhenApiReturnsError()
    {
        var client = BuildClient(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");

        var act = () => client.ExchangeCodeAsync("bad-code", "verifier");

        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExchangeCodeAsync_ThrowsArgumentException_WhenCodeIsInvalid(string code)
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);
        var act = () => client.ExchangeCodeAsync(code, "verifier");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExchangeCodeAsync_ThrowsArgumentException_WhenCodeVerifierIsInvalid(string codeVerifier)
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);
        var act = () => client.ExchangeCodeAsync("auth-code", codeVerifier);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // -------------------------------------------------------------------------
    // RefreshTokenAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_ReturnsNewToken_WhenRefreshTokenExists()
    {
        const string newTokenJson = """
            { "access_token": "new-acc", "refresh_token": "new-ref",
              "token_type": "Bearer", "expires_in": 3600 }
            """;

        var existing = new OAuthTokenResponse
        {
            AccessToken = "old-acc", RefreshToken = "ref-tok",
            ExpiresIn = 3600, ReceivedAt = DateTimeOffset.UtcNow
        };
        var tokenProvider = new FakeTokenProvider(existing);
        var client = BuildClient(HttpStatusCode.OK, newTokenJson, tokenProvider);

        var token = await client.RefreshTokenAsync();

        token.AccessToken.Should().Be("new-acc");
        token.RefreshToken.Should().Be("new-ref");
    }

    [Fact]
    public async Task RefreshTokenAsync_UpdatesStoredToken_AfterRefresh()
    {
        const string newTokenJson = """
            { "access_token": "new-acc", "refresh_token": "new-ref",
              "token_type": "Bearer", "expires_in": 3600 }
            """;

        var existing = new OAuthTokenResponse
        {
            AccessToken = "old-acc", RefreshToken = "ref-tok",
            ExpiresIn = 3600, ReceivedAt = DateTimeOffset.UtcNow
        };
        var tokenProvider = new FakeTokenProvider(existing);
        var client = BuildClient(HttpStatusCode.OK, newTokenJson, tokenProvider);

        await client.RefreshTokenAsync();

        var stored = await tokenProvider.GetTokenAsync();
        stored!.AccessToken.Should().Be("new-acc");
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsInvalidOperationException_WhenNoTokenStored()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson, new FakeTokenProvider(initial: null));

        var act = () => client.RefreshTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sign in again*");
    }

    [Fact]
    public async Task RefreshTokenAsync_ThrowsInvalidOperationException_WhenRefreshTokenIsNull()
    {
        var existing = new OAuthTokenResponse
        {
            AccessToken = "acc", RefreshToken = null,
            ExpiresIn = 3600, ReceivedAt = DateTimeOffset.UtcNow
        };
        var client = BuildClient(HttpStatusCode.OK, TokenJson, new FakeTokenProvider(existing));

        var act = () => client.RefreshTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // -------------------------------------------------------------------------
    // SignOutAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SignOutAsync_ClearsStoredToken()
    {
        var existing = new OAuthTokenResponse
        {
            AccessToken = "acc", RefreshToken = "ref",
            ExpiresIn = 3600, ReceivedAt = DateTimeOffset.UtcNow
        };
        var tokenProvider = new FakeTokenProvider(existing);
        var client = BuildClient(HttpStatusCode.OK, TokenJson, tokenProvider);

        await client.SignOutAsync();

        var stored = await tokenProvider.GetTokenAsync();
        stored.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // RequestPermissionsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestPermissionsAsync_ReturnsDataExchangeToken_WhenApiSucceeds()
    {
        const string responseJson = """{ "token": "dx-tok", "token_type": "data_exchange", "expires_in": 300 }""";
        var tokenProvider = new FakeTokenProvider(MakeStoredToken());
        var client = BuildClient(HttpStatusCode.Created, responseJson, tokenProvider);

        var result = await client.RequestPermissionsAsync(["highlights"]);

        result.Token.Should().Be("dx-tok");
        result.TokenType.Should().Be("data_exchange");
        result.ExpiresIn.Should().Be(300);
    }

    [Fact]
    public async Task RequestPermissionsAsync_SendsStoredAccessTokenAsBearerAndPermissionsInBody()
    {
        const string responseJson = """{ "token": "dx-tok", "token_type": "data_exchange", "expires_in": 300 }""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Created, responseJson);
        var tokenProvider = new FakeTokenProvider(MakeStoredToken());
        var client = BuildClientFromHandler(handler, tokenProvider);

        await client.RequestPermissionsAsync(["highlights"]);

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("acc-tok");
        handler.LastRequest.RequestUri!.Query.Should().Contain("x-yvp-app-key=test-app-key");
        handler.LastRequestBody.Should().Contain("\"requested_permissions\"").And.Contain("highlights");
    }

    [Fact]
    public async Task RequestPermissionsAsync_ThrowsInvalidOperationException_WhenNoTokenStored()
    {
        const string responseJson = """{ "token": "dx-tok", "token_type": "data_exchange", "expires_in": 300 }""";
        var client = BuildClient(HttpStatusCode.Created, responseJson, new FakeTokenProvider(initial: null));

        var act = () => client.RequestPermissionsAsync(["highlights"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sign in*");
    }

    [Fact]
    public async Task RequestPermissionsAsync_ThrowsArgumentException_WhenPermissionsIsEmpty()
    {
        var tokenProvider = new FakeTokenProvider(MakeStoredToken());
        var client = BuildClient(HttpStatusCode.Created, "{}", tokenProvider);

        var act = () => client.RequestPermissionsAsync([]);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RequestPermissionsAsync_ThrowsYouVersionApiException_OnError()
    {
        var tokenProvider = new FakeTokenProvider(MakeStoredToken());
        var client = BuildClient(HttpStatusCode.Unauthorized, """{"error":"invalid_token"}""", tokenProvider);

        var act = () => client.RequestPermissionsAsync(["highlights"]);

        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // BuildDataExchangeApprovalUrl
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildDataExchangeApprovalUrl_ReturnsUrlWithTokenAndAppKeyQueryParameters()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var url = client.BuildDataExchangeApprovalUrl("dx-tok");

        url.AbsoluteUri.Should().Be("https://api.youversion.com/data-exchange?token=dx-tok&x-yvp-app-key=test-app-key");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BuildDataExchangeApprovalUrl_ThrowsArgumentException_WhenTokenIsInvalid(string token)
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var act = () => client.BuildDataExchangeApprovalUrl(token);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildDataExchangeApprovalUrl_ThrowsInvalidOperationException_WhenAppKeyNotConfigured()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson, appKey: "");

        var act = () => client.BuildDataExchangeApprovalUrl("dx-tok");

        act.Should().Throw<InvalidOperationException>().WithMessage("*AppKey*");
    }

    // -------------------------------------------------------------------------
    // ParseDataExchangeCallback
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseDataExchangeCallback_ReturnsGranted_WithPermissions()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var result = client.ParseDataExchangeCallback(
            new Uri("https://myapp.com/?data_exchange_status=granted&granted_permissions=highlights"));

        result.Status.Should().Be(DataExchangeStatus.Granted);
        result.GrantedPermissions.Should().ContainSingle().Which.Should().Be("highlights");
        result.DeniedPermissions.Should().BeEmpty();
    }

    [Fact]
    public void ParseDataExchangeCallback_ReturnsCancelled_WithDeniedPermissionsAndError()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var result = client.ParseDataExchangeCallback(new Uri(
            "https://myapp.com/?data_exchange_status=cancelled&denied_permissions=highlights&error=access_denied"));

        result.Status.Should().Be(DataExchangeStatus.Cancelled);
        result.DeniedPermissions.Should().ContainSingle().Which.Should().Be("highlights");
        result.Error.Should().Be("access_denied");
    }

    [Fact]
    public void ParseDataExchangeCallback_ReturnsError_WithErrorDescription()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var result = client.ParseDataExchangeCallback(new Uri(
            "https://myapp.com/?data_exchange_status=error&error=server_error&error_description=Something%20went%20wrong"));

        result.Status.Should().Be(DataExchangeStatus.Error);
        result.Error.Should().Be("server_error");
        result.ErrorDescription.Should().Be("Something went wrong");
    }

    [Fact]
    public void ParseDataExchangeCallback_ReturnsUnknown_WhenStatusMissing()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var result = client.ParseDataExchangeCallback(new Uri("https://myapp.com/"));

        result.Status.Should().Be(DataExchangeStatus.Unknown);
    }

    [Fact]
    public void ParseDataExchangeCallback_SplitsCommaSeparatedPermissions()
    {
        var client = BuildClient(HttpStatusCode.OK, TokenJson);

        var result = client.ParseDataExchangeCallback(new Uri(
            "https://myapp.com/?data_exchange_status=granted&granted_permissions=highlights,bookmarks"));

        result.GrantedPermissions.Should().BeEquivalentTo(["highlights", "bookmarks"]);
    }

    // -------------------------------------------------------------------------
    // CompleteDataExchangeApprovalAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CompleteDataExchangeApprovalAsync_ReturnsParsedResult_OnSeeOtherResponse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.SeeOther, "", response =>
            response.Headers.Location = new Uri("https://myapp.com/?data_exchange_status=granted&granted_permissions=highlights"));
        var client = BuildClientFromHandler(handler);

        var result = await client.CompleteDataExchangeApprovalAsync("dx-tok");

        result.Status.Should().Be(DataExchangeStatus.Granted);
        result.GrantedPermissions.Should().ContainSingle().Which.Should().Be("highlights");
    }

    [Fact]
    public async Task CompleteDataExchangeApprovalAsync_SendsTokenAndAppKeyQueryParametersWithNoBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.SeeOther, "", response =>
            response.Headers.Location = new Uri("https://myapp.com/?data_exchange_status=granted"));
        var client = BuildClientFromHandler(handler);

        await client.CompleteDataExchangeApprovalAsync("dx-tok");

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.Headers.Authorization.Should().BeNull();
        handler.LastRequest.RequestUri!.Query.Should().Contain("token=dx-tok").And.Contain("x-yvp-app-key=test-app-key");
        handler.LastRequestBody.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteDataExchangeApprovalAsync_ThrowsInvalidOperationException_WhenAppKeyNotConfigured()
    {
        var client = BuildClient(HttpStatusCode.SeeOther, "", appKey: null);

        var act = () => client.CompleteDataExchangeApprovalAsync("dx-tok");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*AppKey*");
    }

    [Fact]
    public async Task CompleteDataExchangeApprovalAsync_ThrowsArgumentException_WhenTokenIsEmpty()
    {
        var client = BuildClient(HttpStatusCode.SeeOther, "");

        var act = () => client.CompleteDataExchangeApprovalAsync("");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CompleteDataExchangeApprovalAsync_ThrowsYouVersionApiException_WhenResponseIsNotSeeOther()
    {
        var client = BuildClient(HttpStatusCode.Unauthorized, """{"error":"invalid_token"}""");

        var act = () => client.CompleteDataExchangeApprovalAsync("dx-tok");

        await act.Should().ThrowAsync<YouVersionApiException>()
            .Where(e => e.StatusCode == HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // OAuthTokenResponse.IsExpired
    // -------------------------------------------------------------------------

    [Fact]
    public void IsExpired_ReturnsFalse_WhenTokenIsFresh()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "tok", ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        token.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenTokenHasExpired()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "tok", ExpiresIn = 10,
            ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-100)
        };

        token.IsExpired().Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenTokenIsWithinBuffer()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "tok", ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-(3600 - 30))
        };

        // 60-second default buffer — token has only 30s remaining
        token.IsExpired(bufferSeconds: 60).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpiresInMissing_ButAccessTokenExists()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = "opaque-access-token",
            ExpiresIn = 0,
            ReceivedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        token.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_UsesJwtExpClaim_WhenExpiresInMissing()
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString();
        var token = new OAuthTokenResponse
        {
            AccessToken = BuildUnsignedJwt("exp", exp),
            ExpiresIn = 0,
            ReceivedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        token.IsExpired(bufferSeconds: 60).Should().BeFalse();
    }

    // -------------------------------------------------------------------------
    // OAuthTokenResponse identity helpers
    // -------------------------------------------------------------------------

    [Fact]
    public void GetDisplayIdentity_ReturnsEmail_WhenNameIsMissing()
    {
        var token = new OAuthTokenResponse
        {
            AccessToken = BuildUnsignedJwt("email", "kevin@example.com"),
            ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        token.GetDisplayIdentity().Should().Be("kevin@example.com");
    }

    [Fact]
    public void GetDisplayIdentity_FallsBackToSubject_WhenNameAndEmailMissing()
    {
        var token = new OAuthTokenResponse
        {
            IdToken = BuildUnsignedJwt("sub", "user-123"),
            AccessToken = "not-a-jwt",
            ExpiresIn = 3600,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        token.GetDisplayIdentity().Should().Be("user-123");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static YouVersionOAuthClient BuildClient(
        HttpStatusCode status,
        string json,
        FakeTokenProvider? tokenProvider = null,
        string? appKey = "test-app-key")
    {
        var handler = new FakeHttpMessageHandler(status, json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://auth.youversion.com")
        };
        var options = Options.Create(new YouVersionOAuthOptions
        {
            ClientId = "test-client",
            RedirectUri = new Uri("https://localhost/callback"),
            AuthorizationEndpoint = new Uri("https://auth.youversion.com/oauth2/authorize"),
            TokenEndpoint = new Uri("https://auth.youversion.com/oauth2/token")
        });
        var apiOptions = Options.Create(new YouVersionApiOptions { AppKey = appKey ?? string.Empty });
        return new YouVersionOAuthClient(
            httpClient,
            options,
            apiOptions,
            tokenProvider ?? new FakeTokenProvider(),
            NullLogger<YouVersionOAuthClient>.Instance);
    }

    private static YouVersionOAuthClient BuildClientFromHandler(
        FakeHttpMessageHandler handler,
        FakeTokenProvider? tokenProvider = null,
        string? appKey = "test-app-key")
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://auth.youversion.com") };
        var options = Options.Create(new YouVersionOAuthOptions
        {
            ClientId = "test-client",
            RedirectUri = new Uri("https://localhost/callback"),
            AuthorizationEndpoint = new Uri("https://auth.youversion.com/oauth2/authorize"),
            TokenEndpoint = new Uri("https://auth.youversion.com/oauth2/token")
        });
        var apiOptions = Options.Create(new YouVersionApiOptions { AppKey = appKey ?? string.Empty });
        return new YouVersionOAuthClient(
            httpClient,
            options,
            apiOptions,
            tokenProvider ?? new FakeTokenProvider(),
            NullLogger<YouVersionOAuthClient>.Instance);
    }

    private static OAuthTokenResponse MakeStoredToken() => new()
    {
        AccessToken = "acc-tok",
        RefreshToken = "ref-tok",
        ExpiresIn = 3600,
        ReceivedAt = DateTimeOffset.UtcNow,
    };

    private static string BuildUnsignedJwt(string claimName, string claimValue)
    {
        var payload = $"{{\"{claimName}\":\"{claimValue}\"}}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var payloadBase64Url = Convert.ToBase64String(payloadBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"header.{payloadBase64Url}.signature";
    }
}
