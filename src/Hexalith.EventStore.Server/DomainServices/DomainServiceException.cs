namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>
/// Exception thrown when a domain service response violates configured limits (AC #6).
/// Examples: too many events, event payload too large.
/// </summary>
public class DomainServiceException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceException"/> class.
    /// </summary>
    public DomainServiceException()
        : base("Domain service invocation error.") {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DomainServiceException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DomainServiceException(string message, Exception innerException)
        : base(message, innerException) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainServiceException"/> class
    /// for a domain service response limit violation.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="domain">The domain name.</param>
    /// <param name="reason">A description of the violation.</param>
    /// <param name="eventCount">The number of events in the response (if applicable).</param>
    /// <param name="eventSizeBytes">The size of the offending event in bytes (if applicable).</param>
    public DomainServiceException(string tenantId, string domain, string reason, int? eventCount = null, int? eventSizeBytes = null)
        : base($"Domain service response rejected for tenant '{tenantId}', domain '{domain}': {reason}") {
        TenantId = tenantId;
        Domain = domain;
        Reason = reason;
        EventCount = eventCount;
        EventSizeBytes = eventSizeBytes;
    }

    /// <summary>Gets the tenant identifier.</summary>
    public string? TenantId { get; }

    /// <summary>Gets the domain name.</summary>
    public string? Domain { get; }

    /// <summary>Gets the reason for the rejection.</summary>
    public string? Reason { get; }

    /// <summary>Gets the event count that triggered the rejection (if applicable).</summary>
    public int? EventCount { get; }

    /// <summary>Gets the event size in bytes that triggered the rejection (if applicable).</summary>
    public int? EventSizeBytes { get; }
}
