using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Platform.API.Configuration;

namespace Platform.API.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects the <c>X-YVP-App-Key</c> and
/// <c>Accept: application/json</c> headers on every outgoing request.
/// </summary>
internal sealed class AppKeyDelegatingHandler : DelegatingHandler
{
    private const string AppKeyHeader = "X-YVP-App-Key";
    private const string AcceptHeader = "Accept";
    private const string JsonMediaType = "application/json";

    private readonly BibleApiOptions _options;

    public AppKeyDelegatingHandler(IOptions<BibleApiOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AppKey))
            throw new InvalidOperationException(
                $"{nameof(BibleApiOptions)}.{nameof(BibleApiOptions.AppKey)} is not configured. " +
                "Set it via AddBibleApiClients(options => options.AppKey = \"your-key\") " +
                "or the BibleApi:AppKey configuration value.");

        request.Headers.TryAddWithoutValidation(AppKeyHeader, _options.AppKey);
        request.Headers.TryAddWithoutValidation(AcceptHeader, JsonMediaType);

        return base.SendAsync(request, cancellationToken);
    }
}
