namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Owns the DAPR service-invocation routing headers on outbound requests.</summary>
internal sealed class DaprServiceInvocationHandler : DelegatingHandler
{
    private readonly Func<(string AppId, string? ApiToken)> _routingFactory;

    internal DaprServiceInvocationHandler(string appId, string? apiToken)
        : this(() => (appId, apiToken))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
    }

    internal DaprServiceInvocationHandler(Func<(string AppId, string? ApiToken)> routingFactory)
    {
        ArgumentNullException.ThrowIfNull(routingFactory);
        _routingFactory = routingFactory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        (string appId, string? apiToken) = _routingFactory();
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (appId.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("The DAPR application id must not contain whitespace or control characters.", nameof(appId));
        }

        _ = request.Headers.Remove("dapr-app-id");
        _ = request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
        _ = request.Headers.Remove("dapr-api-token");
        if (apiToken is { Length: > 0 })
        {
            _ = request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
