using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Computes versioned projection delivery event and prefix fingerprints.</summary>
/// <remarks>
/// The persisted v1 identity intentionally excludes <see cref="ProjectionEventDto.GlobalPosition"/>.
/// Adding that field to v1 would invalidate existing receipts and completed-prefix fingerprints.
/// Existing v1 receipts retain that identity. New mapped routes use v2, which binds the persisted
/// position and optional provenance fields; a full rebuild is the explicit upgrade path for old
/// selected models that need a position watermark.
/// </remarks>
internal static class ProjectionDeliveryFingerprint {
    private static readonly byte[] _eventDomain = "hexalith.projection.delivery.event.v1"u8.ToArray();
    private static readonly byte[] _prefixDomain = "hexalith.projection.delivery.prefix.v1"u8.ToArray();
    private static readonly byte[] _stepDomain = "hexalith.projection.delivery.step.v1"u8.ToArray();
    private static readonly byte[] _eventDomainV2 = "hexalith.projection.delivery.event.v2"u8.ToArray();
    private static readonly byte[] _prefixDomainV2 = "hexalith.projection.delivery.prefix.v2"u8.ToArray();
    private static readonly byte[] _stepDomainV2 = "hexalith.projection.delivery.step.v2"u8.ToArray();

    /// <summary>Computes the empty-prefix fingerprint for one projection scope.</summary>
    public static string ComputeInitial(AggregateIdentity identity, string projectionName, bool positionSensitive = false) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(positionSensitive ? _prefixDomainV2 : _prefixDomain);
        AppendString(hash, identity.TenantId);
        AppendString(hash, identity.Domain);
        AppendString(hash, identity.AggregateId);
        AppendString(hash, projectionName);
        return Format(hash.GetHashAndReset(), positionSensitive);
    }

    /// <summary>Computes the canonical fingerprint of one projection wire event.</summary>
    public static string ComputeEvent(ProjectionEventDto value) => ComputeEvent(value, positionSensitive: false);

    /// <summary>Computes the v2 position- and provenance-sensitive mapped event identity.</summary>
    public static string ComputeEvent(ProjectionEventDto value, bool positionSensitive) {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SequenceNumber <= 0) {
            throw new ArgumentException("Projection event sequence numbers must be positive.", nameof(value));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(value.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.EventTypeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(value.SerializationFormat);
        ArgumentNullException.ThrowIfNull(value.Payload);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(positionSensitive ? _eventDomainV2 : _eventDomain);
        AppendInt64(hash, value.SequenceNumber);
        AppendString(hash, value.MessageId);
        AppendString(hash, value.EventTypeName);
        AppendBytes(hash, value.Payload);
        AppendString(hash, value.SerializationFormat);
        AppendInt64(hash, value.Timestamp.UtcDateTime.Ticks);
        AppendString(hash, value.CorrelationId);
        AppendString(hash, value.UserId);
        if (positionSensitive) {
            AppendInt64(hash, value.GlobalPosition);
            AppendNullableInt64(hash, value.MetadataVersion);
            AppendString(hash, value.StoredEventContractType);
            AppendNullableInt64(hash, value.StoredPayloadVersion);
            AppendString(hash, value.StoredSerializationFormat);
            AppendString(hash, value.EffectiveEventContractType);
            AppendNullableInt64(hash, value.EffectivePayloadVersion);
            AppendString(hash, value.EffectiveSerializationFormat);
            AppendNullableBytes(hash, value.EffectivePayload);
            AppendString(hash, value.StoredEventTypeName);
            AppendNullableBytes(hash, value.StoredDigest);
            AppendString(hash, value.RegistryFingerprint);
            AppendNullableInt64(hash, value.IsAdapted is null ? null : value.IsAdapted.Value ? 1 : 0);
        }
        return Format(hash.GetHashAndReset(), positionSensitive);
    }

    /// <summary>Extends a canonical prefix fingerprint with one canonical event fingerprint.</summary>
    public static string Extend(string prefixFingerprint, string eventFingerprint) {
        bool positionSensitive = prefixFingerprint.StartsWith("v2:", StringComparison.Ordinal);
        if (positionSensitive != eventFingerprint.StartsWith("v2:", StringComparison.Ordinal))
            throw new ArgumentException("Projection delivery fingerprint versions differ.", nameof(eventFingerprint));
        byte[] prefix = Parse(prefixFingerprint);
        byte[] eventDigest = Parse(eventFingerprint);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(positionSensitive ? _stepDomainV2 : _stepDomain);
        hash.AppendData(prefix);
        hash.AppendData(eventDigest);
        return Format(hash.GetHashAndReset(), positionSensitive);
    }

    /// <summary>Validates and fingerprints a positive, contiguous history in its supplied order.</summary>
    public static ProjectionDeliveryFingerprintHistory ComputeHistory(
        AggregateIdentity identity,
        string projectionName,
        IReadOnlyList<ProjectionEventDto> events,
        bool positionSensitive = false) {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0) {
            throw new ArgumentException("Projection delivery history must not be empty.", nameof(events));
        }

        string initial = ComputeInitial(identity, projectionName, positionSensitive);
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
            string eventFingerprint = ComputeEvent(value, positionSensitive);
            prefix = Extend(prefix, eventFingerprint);
            digests.Add(new ProjectionDeliveryEventDigest(value.SequenceNumber, value.MessageId, eventFingerprint, prefix));
            expectedSequence++;
        }

        return new ProjectionDeliveryFingerprintHistory(initial, prefix, digests);
    }

    /// <summary>Computes the same canonical history from bounded readable source pages.</summary>
    public static async Task<ProjectionDeliveryFingerprintHistory> ComputeHistoryAsync(
        AggregateIdentity identity,
        string projectionName,
        IAsyncEnumerable<ProjectionEventDto[]> pages,
        int maximumEvents,
        CancellationToken cancellationToken,
        bool positionSensitive = false) {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEvents);
        string initial = ComputeInitial(identity, projectionName, positionSensitive);
        string prefix = initial;
        var digests = new List<ProjectionDeliveryEventDigest>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        long expectedSequence = 1;
        await foreach (ProjectionEventDto[] page in pages.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            foreach (ProjectionEventDto value in page) {
                if (value is null || value.SequenceNumber != expectedSequence
                    || string.IsNullOrWhiteSpace(value.MessageId)
                    || !identities.Add(value.MessageId)
                    || digests.Count >= maximumEvents) {
                    throw new ArgumentException("Projection delivery history is invalid or exceeds its supported event count.", nameof(pages));
                }

                string eventFingerprint = ComputeEvent(value, positionSensitive);
                prefix = Extend(prefix, eventFingerprint);
                digests.Add(new ProjectionDeliveryEventDigest(value.SequenceNumber, value.MessageId,
                    eventFingerprint, prefix));
                expectedSequence++;
            }
        }

        if (digests.Count == 0)
            throw new ArgumentException("Projection delivery history must not be empty.", nameof(pages));
        return new ProjectionDeliveryFingerprintHistory(initial, prefix, digests);
    }

    /// <summary>Extends one durable accepted prefix from authoritative suffix pages.</summary>
    public static async Task<ProjectionDeliveryFingerprintHistory> ComputeSuffixAsync(
        AggregateIdentity identity, string projectionName, long baseSequence,
        string baseFingerprint, IAsyncEnumerable<ProjectionEventDto[]> pages,
        int maximumEvents, CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFingerprint);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEvents);
        if (baseSequence >= maximumEvents)
            throw new ArgumentException("Projection delivery owner history is over limit.", nameof(baseSequence));
        bool positionSensitive = baseFingerprint.StartsWith("v2:", StringComparison.Ordinal);
        string initial = ComputeInitial(identity, projectionName, positionSensitive);
        string prefix = baseFingerprint;
        long expectedSequence = checked(baseSequence + 1);
        var digests = new List<ProjectionDeliveryEventDigest>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        await foreach (ProjectionEventDto[] page in pages.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            if (page is null || page.Length == 0)
                throw new ArgumentException("Projection delivery suffix has an empty page.", nameof(pages));
            foreach (ProjectionEventDto value in page) {
                if (value is null || value.SequenceNumber != expectedSequence
                    || string.IsNullOrWhiteSpace(value.MessageId)
                    || !identities.Add(value.MessageId)
                    || baseSequence + digests.Count >= maximumEvents)
                    throw new ArgumentException("Projection delivery suffix is invalid or over limit.", nameof(pages));
                string eventFingerprint = ComputeEvent(value, positionSensitive);
                prefix = Extend(prefix, eventFingerprint);
                digests.Add(new ProjectionDeliveryEventDigest(value.SequenceNumber, value.MessageId,
                    eventFingerprint, prefix));
                expectedSequence++;
            }
        }
        if (digests.Count == 0)
            throw new ArgumentException("Projection delivery suffix must not be empty.", nameof(pages));
        return new ProjectionDeliveryFingerprintHistory(initial, prefix, digests,
            baseSequence, baseFingerprint);
    }

    /// <summary>Checks an internally supplied digest chain before it enters durable admission.</summary>
    public static bool IsValidHistory(AggregateIdentity identity, string projectionName,
        ProjectionDeliveryFingerprintHistory? history) {
        if (history is null || history.Events.Count == 0
            || history.BaseSequence > 10_000
            || history.Events.Count > 10_000 - history.BaseSequence
            || !string.Equals(history.InitialFingerprint, ComputeInitial(identity, projectionName,
                history.InitialFingerprint.StartsWith("v2:", StringComparison.Ordinal)), StringComparison.Ordinal))
            return false;
        if (history.BaseSequence < 0 || history.BaseSequence > 0
            && (string.IsNullOrWhiteSpace(history.BaseFingerprint)
                || history.BaseFingerprint.StartsWith("v2:", StringComparison.Ordinal)
                    != history.InitialFingerprint.StartsWith("v2:", StringComparison.Ordinal)))
            return false;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        string prefix = history.BaseSequence == 0 ? history.InitialFingerprint : history.BaseFingerprint!;
        long expected = history.BaseSequence + 1;
        try {
            foreach (ProjectionDeliveryEventDigest value in history.Events) {
                if (value.SequenceNumber != expected || string.IsNullOrWhiteSpace(value.MessageId)
                    || !identities.Add(value.MessageId)) return false;
                prefix = Extend(prefix, value.EventFingerprint);
                if (!string.Equals(prefix, value.PrefixFingerprint, StringComparison.Ordinal)) return false;
                expected++;
            }
        }
        catch (ArgumentException) { return false; }
        return string.Equals(prefix, history.PrefixFingerprint, StringComparison.Ordinal);
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

    private static void AppendNullableInt64(IncrementalHash hash, long? value) {
        AppendInt32(hash, value is null ? 0 : 1);
        if (value is not null) AppendInt64(hash, value.Value);
    }

    private static void AppendNullableBytes(IncrementalHash hash, byte[]? value) {
        if (value is null) AppendInt32(hash, -1);
        else AppendBytes(hash, value);
    }

    private static string Format(byte[] digest, bool positionSensitive = false) =>
        (positionSensitive ? "v2:" : "v1:")
        + Convert.ToBase64String(digest).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Parse(string fingerprint) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (!fingerprint.StartsWith("v1:", StringComparison.Ordinal)
            && !fingerprint.StartsWith("v2:", StringComparison.Ordinal)) {
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
