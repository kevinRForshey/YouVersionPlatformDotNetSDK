using System.Net;
using System.Text;

namespace Platform.API.Benchmarks;

/// <summary>An <see cref="HttpMessageHandler"/> that returns the same canned JSON body for every request.</summary>
internal sealed class StaticJsonHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
}

/// <summary>An <see cref="HttpMessageHandler"/> that returns an empty 200 OK with no I/O — isolates handler overhead.</summary>
internal sealed class NoopHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
}
