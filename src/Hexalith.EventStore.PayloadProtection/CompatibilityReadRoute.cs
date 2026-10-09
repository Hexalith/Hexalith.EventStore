// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 12 and 13.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Identifies the single compatibility route selected for one stored event or snapshot (normative section 12.1).
/// </summary>
internal enum CompatibilityReadRoute
{
    /// <summary>
    /// No route executed: the carrier, format, shape, reader registration, or snapshot type registration failed
    /// locally, so no key resolver or legacy reader was called.
    /// </summary>
    Rejected = 0,

    /// <summary>The carrier is missing: legacy plaintext pass-through with the stored format preserved.</summary>
    LegacyUnprotected = 1,

    /// <summary>The carrier is explicitly unprotected: plaintext pass-through with the stored format preserved.</summary>
    Unprotected = 2,

    /// <summary>The stored format is exactly <c>json-redacted</c>: redacted pass-through that is never re-protected.</summary>
    Redacted = 3,

    /// <summary>The stored record is <c>json+pdenc-v1</c>: the registered <c>parties-pdenc-v1</c> reader decides.</summary>
    RegisteredV1 = 4,

    /// <summary>The stored record is <c>json+pdenc-v2</c>: the shared Story 8.3 core authenticates it.</summary>
    SharedV2 = 5,
}
