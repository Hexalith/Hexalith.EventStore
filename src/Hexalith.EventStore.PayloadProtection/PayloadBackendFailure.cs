// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10 and 15.
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
