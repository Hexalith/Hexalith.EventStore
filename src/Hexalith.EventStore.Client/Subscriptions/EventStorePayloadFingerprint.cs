using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Subscriptions;

/// <summary>
/// Computes the stable fingerprint of an event payload's exact persisted bytes.
/// </summary>
/// <remarks>
/// Delivered envelopes and authoritative stream reads use the same function, so a consumer can prove that a
/// delivery at one aggregate sequence carries exactly the bytes that EventStore persisted at that sequence.
/// </remarks>
public static class EventStorePayloadFingerprint {
    /// <summary>
    /// Computes the lowercase hexadecimal SHA-256 digest of <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">The exact payload bytes.</param>
    /// <returns>A 64-character lowercase hexadecimal digest.</returns>
    public static string Compute(ReadOnlySpan<byte> payload)
        => Convert.ToHexStringLower(SHA256.HashData(payload));

    /// <summary>Computes a content fingerprint over the admitted envelope and exact persisted payload bytes.</summary>
    /// <param name="envelope">The domain-event envelope.</param>
    /// <returns>A 64-character lowercase hexadecimal digest.</returns>
    public static string ComputeDelivery(EventStoreDomainEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Compute(JsonSerializer.SerializeToUtf8Bytes(new
        {
            envelope.MessageId,
            envelope.TenantId,
            envelope.Domain,
            envelope.AggregateId,
            envelope.AggregateType,
            envelope.EventTypeName,
            envelope.SequenceNumber,
            envelope.GlobalPosition,
            Timestamp = envelope.Timestamp.ToUniversalTime(),
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.UserId,
            envelope.DomainServiceVersion,
            MetadataVersion = envelope.MetadataVersion ?? 1,
            PayloadVersion = envelope.PayloadVersion ?? 1,
            envelope.SerializationFormat,
            Payload = Compute(envelope.Payload),
            envelope.EventContractType,
            envelope.StoredEventContractType,
            envelope.StoredPayloadVersion,
            envelope.StoredSerializationFormat,
            envelope.StoredEventTypeName,
            StoredDigest = envelope.StoredDigest is { } digest ? Compute(digest) : null,
            envelope.ReadableSerializationFormat,
            envelope.EffectiveEventContractType,
            envelope.EffectivePayloadVersion,
            envelope.EffectiveSerializationFormat,
            EffectivePayload = envelope.EffectivePayload is { } effective ? Compute(effective) : null,
            envelope.RegistryFingerprint,
            envelope.IsAdapted,
            ReadableExtensions = envelope.ReadableExtensions?.OrderBy(item => item.Key, StringComparer.Ordinal),
            envelope.VerifiedEffectiveEvent,
        }));
    }
}
