namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Describes the current domain-owned erasure state observed by payload protection.
/// Implements Story 8.1 sections 9 and 10 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public enum PayloadErasureState {
    /// <summary>Reads and protected writes are permitted.</summary>
    Active = 0,

    /// <summary>Reads are permitted and new protected writes are blocked.</summary>
    Pending = 1,

    /// <summary>Reads and writes are blocked while invalidation executes.</summary>
    Invalidating = 2,

    /// <summary>Reads and writes are blocked after accepted invalidation.</summary>
    Invalidated = 3,

    /// <summary>Reads and writes are blocked after deletion.</summary>
    Deleted = 4,

    /// <summary>The provider cannot establish a consistent state.</summary>
    Unknown = 5,

    /// <summary>The erasure-state provider is temporarily unavailable.</summary>
    Unavailable = 6,

    /// <summary>The erasure-state provider denied the operation.</summary>
    Denied = 7,
}
