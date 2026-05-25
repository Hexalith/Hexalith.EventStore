namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Routes outgoing Admin API requests through the local DAPR sidecar using
/// <c>dapr-app-id</c> header-based service invocation (D13, supersedes the ADR-P4
/// HTTP deviation).
/// </summary>
/// <remarks>
/// The named "AdminApi" client's <c>BaseAddress</c> points at this app's own DAPR
/// sidecar (<c>http://localhost:{DAPR_HTTP_PORT}</c>), so the request path is preserved
/// verbatim and DAPR forwards it to the target app named by <paramref name="appId"/>.
/// Header-based routing is used instead of <c>Dapr.AspNetCore.InvocationHandler</c>
/// (host-name rewriting) because the latter collides with the global
/// <c>AddServiceDiscovery()</c> default applied by ServiceDefaults — a literal
/// <c>localhost</c> base address is a no-op for the service-discovery resolver.
/// The <c>Authorization</c> bearer header set by <see cref="AdminApiAuthorizationHandler"/>
/// is forwarded by DAPR unchanged, so Admin.Server's JWT/RBAC/tenant enforcement is preserved.
/// </remarks>
/// <param name="appId">The DAPR application id of the invocation target (e.g. <c>eventstore-admin</c>).</param>
/// <param name="apiToken">
/// Optional DAPR API token (<c>DAPR_API_TOKEN</c>). When set, it is sent as the
/// <c>dapr-api-token</c> header so the sidecar accepts the app-to-sidecar call in
/// token-authenticated deployments. Null/empty in self-hosted dev.
/// </param>
public sealed class DaprAppIdHandler(string appId, string? apiToken) : DelegatingHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.Headers.TryAddWithoutValidation("dapr-app-id", appId);
        if (!string.IsNullOrEmpty(apiToken)) {
            _ = request.Headers.TryAddWithoutValidation("dapr-api-token", apiToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
