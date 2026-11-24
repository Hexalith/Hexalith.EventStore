using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.Sample.Api.Services;

/// <summary>
/// Forwards the inbound caller's <c>Authorization</c> bearer header to outgoing EventStore gateway
/// calls, so EventStore's central JWT/RBAC/tenant enforcement stays authoritative for external API
/// requests. This external host validates the caller's token inbound and delegates authorization to
/// the gateway — it never mints its own credentials. Only a single inbound header value is forwarded: repeated
/// values would be comma-joined into a malformed bearer. The host's inbound JWT validation reads that same joined value
/// and rejects the request with 401 before a generated controller runs, so this guard keeps the handler itself from
/// ever emitting a joined credential.
/// </summary>
public sealed class InboundBearerForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StringValues authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization ?? StringValues.Empty;
        if (authorization.Count == 1 && !string.IsNullOrWhiteSpace(authorization[0]))
        {
            _ = request.Headers.TryAddWithoutValidation("Authorization", authorization[0]);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
