using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Computes the frozen v1 projection delivery event and prefix fingerprints.</summary>
internal static class ProjectionDeliveryFingerprint {
    private static readonly byte[] _eventDomain = "hexalith.projection.delivery.event.v1"u8.ToArray();
    private static readonly byte[] _prefixDomain = "hexalith.projection.delivery.prefix.v1"u8.ToArray();
    private static readonly byte[] _stepDomain = "hexalith.projection.delivery.step.v1"u8.ToArray();

    /// <summary>Computes the empty-prefix fingerprint for one projection scope.</summary>
    public static string ComputeInitial(AggregateIdentity identity, string projectionName) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(_prefixDomain);
        AppendString(hash, identity.TenantId);
        AppendString(hash, identity.Domain);
        AppendString(hash, identity.AggregateId);
        AppendString(hash, projectionName);
        return Format(hash.GetHashAndReset());
    }

    /// <summary>Computes the canonical fingerprint of one projection wire event.</summary>
    public static string ComputeEvent(ProjectionEventDto value) {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SequenceNumber <= 0) {
            throw new ArgumentException("Projection event sequence numbers must be positive.", nameof(value));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EventTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SerializationFormat);
        ArgumentNullException.ThrowIfNull(value.Payload);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(_eventDomain);
        AppendInt64(hash, value.SequenceNumber);
        AppendString(hash, value.MessageId);
        AppendString(hash, value.EventTypeName);
        AppendBytes(hash, value.Payload);
        AppendString(hash, value.SerializationFormat);
        AppendInt64(hash, value.Timestamp.UtcDateTime.Ticks);
        AppendString(hash, value.CorrelationId);
        AppendString(hash, value.UserId);
        return Format(hash.GetHashAndReset());
    }

    /// <summary>Extends a canonical prefix fingerprint with one canonical event fingerprint.</summary>
    public static string Extend(string prefixFingerprint, string eventFingerprint) {
        byte[] prefix = Parse(prefixFingerprint);
        byte[] eventDigest = Parse(eventFingerprint);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(_stepDomain);
        hash.AppendData(prefix);
        hash.AppendData(eventDigest);
        return Format(hash.GetHashAndReset());
    }

    /// <summary>Validates and fingerprints a positive, contiguous history in its supplied order.</summary>
    public static ProjectionDeliveryFingerprintHistory ComputeHistory(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events) {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) {
            throw new ArgumentException("Projection delivery history must not be empty.", nameof(events));
        }

        string initial = ComputeInitial(identity, projectionName);
        string prefix = initial;
        var digests = new List<ProjectionDeliveryEventDigest>(events.Count);
        long expectedSequence = 1;
        var identities = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (ProjectionEventDto value in events) {
            ArgumentNullException.ThrowIfNull(value);
            if (value.SequenceNumber != expectedSequence) {
                throw new ArgumentException("Projection delivery history must be strictly increasing and contiguous from sequence one.", nameof(events));
            }

            if (string.IsNullOrWhiteSpace(value.MessageId)) {
                throw new ArgumentException("Projection delivery history requires a persisted message identity for every event.", nameof(events));
            }

            if (identities.TryGetValue(value.MessageId, out long priorSequence) && priorSequence != value.SequenceNumber) {
                throw new ArgumentException("Projection delivery history contains a repeated message identity at a different sequence.", nameof(events));
            }

            identities[value.MessageId] = value.SequenceNumber;
            string eventFingerprint = ComputeEvent(value);
            prefix = Extend(prefix, eventFingerprint);
            digests.Add(new ProjectionDeliveryEventDigest(value.SequenceNumber, value.MessageId, eventFingerprint, prefix));
            expectedSequence++;
        }

        return new ProjectionDeliveryFingerprintHistory(initial, prefix, digests);
    }

    private static void AppendBytes(IncrementalHash hash, byte[] value) {
        AppendInt32(hash, value.Length);
        hash.AppendData(value);
    }

    private static void AppendString(IncrementalHash hash, string? value) {
        if (value is null) {
            AppendInt32(hash, -1);
            return;
        }

        int byteCount = Encoding.UTF8.GetByteCount(value);
        AppendInt32(hash, byteCount);
        if (byteCount == 0) {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value) {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value) {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static string Format(byte[] digest) =>
        "v1:" + Convert.ToBase64String(digest).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Parse(string fingerprint) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (!fingerprint.StartsWith("v1:", StringComparison.Ordinal)) {
            throw new ArgumentException("Projection delivery fingerprint has an unsupported version.", nameof(fingerprint));
        }

        string encoded = fingerprint[3..].Replace('-', '+').Replace('_', '/');
        encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
        try {
            byte[] digest = Convert.FromBase64String(encoded);
            if (digest.Length != SHA256.HashSizeInBytes) {
                throw new ArgumentException("Projection delivery fingerprint has an invalid length.", nameof(fingerprint));
            }

            return digest;
        }
        catch (FormatException exception) {
            throw new ArgumentException("Projection delivery fingerprint is malformed.", nameof(fingerprint), exception);
        }
    }
}
