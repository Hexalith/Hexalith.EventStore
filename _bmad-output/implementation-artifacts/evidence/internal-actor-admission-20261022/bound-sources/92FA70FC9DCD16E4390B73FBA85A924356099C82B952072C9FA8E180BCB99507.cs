namespace Hexalith.EventStore.DomainService;

/// <summary>Represents a support-safe malformed or unsupported v2 projection dispatch request.</summary>
internal sealed class ProjectionDispatchValidationException : Exception {
    /// <summary>Initializes a new validation exception with a stable support-safe reason code.</summary>
    /// <param name="reasonCode">The stable reason code.</param>
    public ProjectionDispatchValidationException(string reasonCode)
        : base(reasonCode) {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ReasonCode = reasonCode;
    }

    /// <summary>Gets the stable support-safe reason code.</summary>
    public string ReasonCode { get; }
}
