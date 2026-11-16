using System.Net;
using System.Text;
using Shouldly;
using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Security;

/// <summary>Models the production HTTP boundary in private Redis ownership tests.</summary>
internal sealed class WriterProtocolTestCutoverHandler(IDatabase database, string sourceCommit, HttpStatusCode activationStatus = HttpStatusCode.OK) : HttpMessageHandler {
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        if (request.Method == HttpMethod.Get
            && string.Equals(request.RequestUri?.AbsolutePath, "/health", StringComparison.Ordinal)) {
            return JsonResponse(
                HttpStatusCode.ServiceUnavailable,
                "{\"results\":{\"projection-delivery-writer-protocol\":{\"status\":\"Unhealthy\"},"
                + "\"redis\":{\"status\":\"Healthy\"}}}");
        }

        if (request.Method == HttpMethod.Post
            && string.Equals(
                request.RequestUri?.AbsolutePath,
                "/api/v1/admin/projections/delivery-writer-protocol/activate",
                StringComparison.Ordinal)) {
            if (activationStatus != HttpStatusCode.OK) return JsonResponse(activationStatus, "activation refused");
            request.Headers.Authorization?.Parameter.ShouldBe("admin-token");
            _ = await database.HashSetAsync(
                "projection-delivery-writer-protocol",
                "data",
                $"{{\"schemaVersion\":1,\"writerProtocolVersion\":2,\"cutoverCommit\":\"{sourceCommit}\"}}")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return JsonResponse(HttpStatusCode.OK, "{\"status\":\"Activated\"}");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode) {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
