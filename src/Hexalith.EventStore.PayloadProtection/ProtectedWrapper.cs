namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Associates one locally validated wrapper location with its parsed envelope.
/// </summary>
/// <param name="Path">The canonical wrapper path.</param>
/// <param name="Envelope">The parsed binary envelope.</param>
internal sealed record ProtectedWrapper(string Path, PayloadProtectionEnvelope Envelope);
