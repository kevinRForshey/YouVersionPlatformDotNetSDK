using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.API.Tests.Fakes;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that returns a configurable response.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public FakeHttpMessageHandler(HttpStatusCode statusCode, string jsonBody)
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>The last request received by this handler.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>
    /// The last request's content, read eagerly since the caller may dispose its
    /// <see cref="HttpContent"/> before the test gets a chance to inspect it.
    /// </summary>
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return _response;
    }

    protected override void Dispose(bool disposing)
    {
        // Do not dispose _response here; callers hold it through the lifetime of the test.
    }
}
