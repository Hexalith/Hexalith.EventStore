using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Deterministic transport response for source certificate substitution tests.</summary>
/// <param name="result">The candidate authenticated owner result.</param>
internal sealed class RetainedHistoryResponseHandler(RetainedIdentityHistoryReadResult result) : HttpMessageHandler
{
    /// <summary>Returns the configured candidate over the expected internal route.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage(request.RequestUri?.AbsolutePath == "/api/v1/identity-history/read"
            ? HttpStatusCode.OK : HttpStatusCode.NotFound) { Content = JsonContent.Create(result) });
    }
}
