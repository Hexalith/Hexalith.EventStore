using System.Net;
using System.Net.Http.Json;

using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.QueryRouting.Tests;

internal sealed class QueryRequestCaptureHandler : HttpMessageHandler {
    public string? Authorization { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        Authorization = request.Headers.TryGetValues("Authorization", out IEnumerable<string>? values)
            ? values.Single()
            : null;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(QueryResult.Failure("test-response")),
        });
    }
}
