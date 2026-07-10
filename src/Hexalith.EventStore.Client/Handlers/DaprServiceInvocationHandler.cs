namespace Hexalith.EventStore.Client.Handlers;

/// <summary>
/// Owns the DAPR service-invocation routing headers on outbound requests.
/// </summary>
internal sealed class DaprServiceInvocationHandler(string appId, string? apiToken) : DelegatingHandler {
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        _ = request.Headers.Remove("dapr-app-id");
        _ = request.Headers.TryAddWithoutValidation("dapr-app-id", appId);

        _ = request.Headers.Remove("dapr-api-token");
        if (apiToken is { Length: > 0 }) {
            _ = request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
