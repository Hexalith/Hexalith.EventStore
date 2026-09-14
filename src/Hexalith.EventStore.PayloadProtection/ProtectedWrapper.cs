namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Associates one locally validated wrapper location with its parsed envelope.
/// </summary>
/// <param name="Path">The canonical wrapper path.</param>
/// <param name="Envelope">The parsed binary envelope.</param>
/// <param name="Start">The wrapper object's first byte offset.</param>
/// <param name="Length">The wrapper object's byte length.</param>
internal sealed record ProtectedWrapper(
    string Path,
    PayloadProtectionEnvelope Envelope,
    int Start,
    int Length);
