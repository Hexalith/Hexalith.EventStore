namespace Hexalith.EventStore.Sample.Api.Services;

/// <summary>
/// Forwards the inbound caller's <c>Authorization</c> bearer header to outgoing EventStore gateway
/// calls, so EventStore's central JWT/RBAC/tenant enforcement stays authoritative for external API
/// requests. This external host validates the caller's token inbound and delegates authorization to
/// the gateway — it never mints its own credentials.
/// </summary>
public sealed class InboundBearerForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization;
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            _ = request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
