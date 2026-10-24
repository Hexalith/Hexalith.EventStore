// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 8-10, 13-15; lifecycle amendment sections 1-7.
// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 10, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines the closed low-cardinality core diagnostic operations from normative section 10.8.
/// </summary>
internal enum PayloadProtectionOperation
{
    /// <summary>Payload protection.</summary>
    Protect = 1,

    /// <summary>Payload authentication and unprotection.</summary>
    Unprotect = 2,

    /// <summary>Opaque backend wrapping.</summary>
    Wrap = 3,
    /// <summary>Exact-version backend unwrapping.</summary>
    Unwrap = 4,
    /// <summary>KEK inventory rotation.</summary>
    Rewrap = 5,
    /// <summary>Online scope invalidation or deletion.</summary>
    Invalidate = 6,
    /// <summary>Claim-fenced durable recovery.</summary>
    Reconcile = 7,
}
