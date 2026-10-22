using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Injected current key delivery. Production passes none: wrap and unwrap stay refused, and a missing delivery denies before ciphertext.
/// </summary>
public interface IInteractionOccurrenceKeyDelivery
{
    /// <summary>Returns the current delivery result for one exact purpose, or null when this delivery is closed.</summary>
    /// <param name="identity">Exact occurrence identity.</param>
    /// <param name="purpose">Exact <c>tenant-digest</c> or <c>interaction-root</c> purpose.</param>
    /// <param name="version">Exact retained version.</param>
    /// <param name="keyReference">Occurrence key reference when the caller already has one.</param>
    /// <param name="writing">Whether the caller holds a write attempt.</param>
    /// <param name="cancellationToken">Operation cancellation.</param>
    /// <returns>The current result, or null when no key may leave.</returns>
    Task<InteractionOccurrenceKeyDeliveryResult?> DeliverAsync(
        InteractionOccurrenceIdentity identity,
        string purpose,
        string version,
        string? keyReference,
        bool writing,
        CancellationToken cancellationToken);
}
