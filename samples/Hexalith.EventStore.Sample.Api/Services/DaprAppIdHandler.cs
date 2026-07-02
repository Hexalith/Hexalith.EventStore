namespace Hexalith.EventStore.Sample.Api.Services;

/// <summary>
/// Routes outgoing EventStore requests through the local DAPR sidecar using <c>dapr-app-id</c>
/// header-based service invocation. The client's <c>BaseAddress</c> points at this app's own DAPR
/// sidecar (<c>http://localhost:{DAPR_HTTP_PORT}</c>), so the request path is preserved verbatim and
/// DAPR forwards it to the target app named by <paramref name="appId"/>. Header-based routing is used
/// instead of <c>Dapr.AspNetCore.InvocationHandler</c> (host-name rewriting) because the latter
/// collides with the global <c>AddServiceDiscovery()</c> default applied by ServiceDefaults.
/// </summary>
/// <param name="appId">The DAPR application id of the invocation target (<c>eventstore</c>).</param>
/// <param name="apiToken">
/// Optional DAPR API token (<c>DAPR_API_TOKEN</c>); sent as <c>dapr-api-token</c> when set.
/// </param>
public sealed class DaprAppIdHandler(string appId, string? apiToken) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
        if (!string.IsNullOrEmpty(apiToken))
        {
            _ = request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
