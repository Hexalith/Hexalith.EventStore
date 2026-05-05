namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Thrown when an upstream tenant query pipeline returns a failed envelope
/// (<c>SubmitQueryResponse.Success == false</c>) with an error message that the admin
/// server cannot semantically classify into 403/404. Distinct from transport/service
/// availability failures so <see cref="System.Net.HttpStatusCode.BadGateway"/> never
/// gets confused with 503 in <c>AdminTenantsController.IsServiceUnavailable</c>.
/// </summary>
internal sealed class TenantQueryFailedException : Exception {
    public TenantQueryFailedException(string upstreamMessage)
        : base($"Tenant query failed upstream: '{upstreamMessage}'.") => UpstreamMessage = upstreamMessage;

    public string UpstreamMessage { get; }
}
