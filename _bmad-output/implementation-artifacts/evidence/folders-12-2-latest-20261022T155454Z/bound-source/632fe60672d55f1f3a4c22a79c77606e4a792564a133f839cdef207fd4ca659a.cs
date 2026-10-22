using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.EventStore.Server.Events;

/// <summary>Prevents self-reporting no-op wrappers from selecting the engine's sealed distribution or erased replay route.</summary>
internal static class SealedPayloadDistributionGuard
{
    /// <summary>Requires the actual sealed engine and exact unchanged retained representation before trusting sealed distribution.</summary>
    internal static void RequireSealed(IEventPayloadProtectionService service, EventEnvelope envelope,
        EventStorePayloadProtectionMetadata metadata, PayloadDistributionResult result)
    {
        if (service is not InteractionPayloadProtectionService || !IsEngineEnvelope(envelope, metadata)
            || envelope.ApplicationPayloadDigest is null
            || result.Outcome.IsUnreadable || result.Outcome.SerializationFormat != envelope.SerializationFormat
            || result.Outcome.Metadata != metadata || result.Outcome.PayloadBytes is null
            || !result.Outcome.PayloadBytes.AsSpan().SequenceEqual(envelope.Payload))
        { throw new InvalidOperationException("SealedDistributionMismatch"); }
    }

    /// <summary>Identifies exact v2 engine representations; arbitrary provider-opaque or legacy records never gain relaxed plaintext digest rules.</summary>
    internal static bool IsEngineEnvelope(EventEnvelope envelope, EventStorePayloadProtectionMetadata metadata)
        => envelope.SerializationFormat == "json+pdenc-v2" && metadata.State == PayloadProtectionState.Protected
            && metadata.Scheme == "hexalith-pdenc-v2";
}
