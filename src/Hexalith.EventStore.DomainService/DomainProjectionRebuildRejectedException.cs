namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Signals that a named projection deterministically rejected rebuild input and retrying it unchanged cannot
/// succeed.
/// </summary>
public sealed class DomainProjectionRebuildRejectedException : Exception {
    /// <summary>Initializes a terminal rebuild rejection with a safe projection reason code.</summary>
    /// <param name="reasonCode">The bounded public reason code returned by the dispatcher.</param>
    public DomainProjectionRebuildRejectedException(string reasonCode)
        : base("The named projection rejected the rebuild input.") {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ReasonCode = reasonCode;
    }

    /// <summary>Gets the safe projection reason code returned by the dispatcher.</summary>
    public string ReasonCode { get; }
}
