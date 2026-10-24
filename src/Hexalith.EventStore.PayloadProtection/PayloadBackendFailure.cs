// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10 and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The provider-neutral constructive backend outcome; arbitrary provider text is never carried.</summary>
internal enum PayloadBackendFailure
{
    /// <summary>The operation completed and its output is available.</summary>
    None,
    /// <summary>A transient failure occurred with a confirmed absent mutation.</summary>
    Unavailable,
    /// <summary>Authorization or authentication was denied.</summary>
    Denied,
    /// <summary>The exact provider version is absent.</summary>
    Missing,
    /// <summary>Current lifecycle evidence proves invalidation.</summary>
    Invalidated,
    /// <summary>The provider result disagrees with validated state.</summary>
    ConsistencyMismatch,
    /// <summary>A mutation may have occurred and must be reconciled by operation ID before replay.</summary>
    Ambiguous,
}
