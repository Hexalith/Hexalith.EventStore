namespace Hexalith.EventStore.Server.Pipeline.Commands;

/// <summary>
/// Exception raised when a command completes with a domain rejection event.
/// This allows HTTP hosts to surface RFC 7807 responses instead of returning 202 Accepted.
/// </summary>
public class DomainCommandRejectedException : InvalidOperationException {
    public DomainCommandRejectedException(string correlationId, string tenantId, string rejectionType, string detail)
        : base(detail) {
        CorrelationId = correlationId;
        TenantId = tenantId;
        RejectionType = rejectionType;
        Detail = detail;
    }

    public string CorrelationId { get; }

    public string TenantId { get; }

    public string RejectionType { get; }

    public string Detail { get; }
}
