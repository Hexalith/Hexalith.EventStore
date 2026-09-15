// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6-8, 14, and 15.
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
    int Length)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(ProtectedWrapper);
}
