namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Provides a bounded constructive reason for an observed payload-erasure state.
/// Implements Story 8.1 sections 9 and 10 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public enum PayloadErasureReasonCode {
    /// <summary>No blocking erasure reason applies.</summary>
    None = 0,

    /// <summary>The domain reports erasure pending.</summary>
    DomainPending = 1,

    /// <summary>The domain reports invalidation in progress.</summary>
    DomainInvalidating = 2,

    /// <summary>The domain reports invalidation complete.</summary>
    DomainInvalidated = 3,

    /// <summary>The domain reports deletion.</summary>
    DomainDeleted = 4,

    /// <summary>The state provider is unavailable.</summary>
    StateUnavailable = 5,

    /// <summary>The state provider denied the query.</summary>
    StateDenied = 6,

    /// <summary>The state provider cannot establish a state.</summary>
    StateUnknown = 7,

    /// <summary>The lifecycle epoch is invalid for the observed transition.</summary>
    EpochInvalid = 8,

    /// <summary>The lifecycle epoch regressed.</summary>
    EpochRegressed = 9,
}
