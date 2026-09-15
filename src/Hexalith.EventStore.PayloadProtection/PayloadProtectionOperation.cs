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
}
