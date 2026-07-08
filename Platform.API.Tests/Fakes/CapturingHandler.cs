using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.API.Tests.Fakes;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that captures the last outgoing
/// request and the number of requests sent, returning a configurable response.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string? _responseBody;

    public CapturingHandler(HttpStatusCode statusCode = HttpStatusCode.OK, string? responseBody = null)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    /// <summary>The last request received by this handler.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The number of requests received by this handler.</summary>
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        CallCount++;

        var response = new HttpResponseMessage(_statusCode);
        if (_responseBody is not null)
            response.Content = new StringContent(_responseBody, Encoding.UTF8, "application/json");

        return Task.FromResult(response);
    }
}
