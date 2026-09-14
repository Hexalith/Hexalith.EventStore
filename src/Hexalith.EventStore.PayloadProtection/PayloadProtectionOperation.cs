namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines the closed low-cardinality core diagnostic operations from normative section 10.8.
/// </summary>
internal enum PayloadProtectionOperation {
    /// <summary>Payload protection.</summary>
    Protect = 1,

    /// <summary>Payload authentication and unprotection.</summary>
    Unprotect = 2,
}
