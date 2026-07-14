using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.API.Tests.Fakes;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that returns a queued sequence of
/// responses, one per request, for exercising multi-hop client flows.
/// </summary>
internal sealed class SequencedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public SequencedHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
    }

    /// <summary>All requests received by this handler, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
            throw new InvalidOperationException("No more queued responses.");

        var response = _responses.Dequeue();
        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    protected override void Dispose(bool disposing)
    {
        // Do not dispose queued responses here; callers hold them through the lifetime of the test.
    }
}
