namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Owns the DAPR service-invocation routing headers on outbound requests.</summary>
internal sealed class DaprServiceInvocationHandler : DelegatingHandler
{
    private readonly Func<(string AppId, string? ApiToken)> _routingFactory;

    internal DaprServiceInvocationHandler(string appId, string? apiToken)
        : this(() => (appId, apiToken))
    {
        ValidateAppId(appId);
    }

    internal DaprServiceInvocationHandler(Func<(string AppId, string? ApiToken)> routingFactory)
    {
        ArgumentNullException.ThrowIfNull(routingFactory);
        _routingFactory = routingFactory;
    }

    /// <summary>Validates the same target syntax for fixed and per-send SDK routing.</summary>
    internal static void ValidateAppId(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        if (appId.Any(static character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '.' and not '_'))
        {
            throw new ArgumentException("The DAPR application id must contain only ASCII letters, digits, hyphens, dots or underscores.", nameof(appId));
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        (string appId, string? apiToken) = _routingFactory();
        ValidateAppId(appId);

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
