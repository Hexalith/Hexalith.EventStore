using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Computes the canonical owner-history fingerprint from bounded event pages.</summary>
public sealed class DomainSharedProjectionHistoryFingerprintBuilder : IDisposable
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly long _expectedCount;
    private readonly bool _legacyVersion;
    private long _count;
    private bool _finished;

    /// <summary>Begins one owner history with its sealed event count.</summary>
    /// <param name="aggregateId">The canonical aggregate identifier.</param>
    /// <param name="isErased">Whether the source owner is completed-erased.</param>
    /// <param name="expectedCount">The exact number of event envelopes in the history.</param>
    /// <param name="expectedFingerprint">An existing v1 or v2 receipt whose version must be preserved on retry.</param>
    public DomainSharedProjectionHistoryFingerprintBuilder(
        string aggregateId, bool isErased, long expectedCount, string? expectedFingerprint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);
        _expectedCount = expectedCount;
        _legacyVersion = expectedFingerprint?.StartsWith("v1:", StringComparison.Ordinal) == true;
        if (!_legacyVersion)
            _hash.AppendData("hexalith-shared-projection-history-v2"u8);
        DomainSharedProjectionRebuildFingerprint.AppendString(_hash, aggregateId);
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, isErased ? 1 : 0);
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, expectedCount);
    }

    /// <summary>Appends the next exact stored event in aggregate-local sequence order.</summary>
    public void Append(ProjectionEventDto item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_finished || _count >= _expectedCount)
            throw new InvalidOperationException("Owner history exceeds its sealed event count.");
        DomainSharedProjectionRebuildFingerprint.AppendString(_hash, item.EventTypeName);
        DomainSharedProjectionRebuildFingerprint.AppendBytes(_hash, item.Payload);
        DomainSharedProjectionRebuildFingerprint.AppendString(_hash, item.SerializationFormat);
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, item.SequenceNumber);
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, item.Timestamp.UtcTicks);
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, (long)item.Timestamp.Offset.TotalMinutes);
        DomainSharedProjectionRebuildFingerprint.AppendString(_hash, item.CorrelationId);
        DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.MessageId);
        DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.UserId);
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, item.GlobalPosition);
        if (!_legacyVersion)
        {
            AppendNullableInt64(item.MetadataVersion);
            DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.StoredEventContractType);
            AppendNullableInt64(item.StoredPayloadVersion);
            DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.StoredSerializationFormat);
            DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.EffectiveEventContractType);
            AppendNullableInt64(item.EffectivePayloadVersion);
            DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.EffectiveSerializationFormat);
            AppendNullableBytes(item.EffectivePayload);
            DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.StoredEventTypeName);
            AppendNullableBytes(item.StoredDigest);
            DomainSharedProjectionRebuildFingerprint.AppendNullableString(_hash, item.RegistryFingerprint);
            AppendNullableInt64(item.IsAdapted is null ? null : item.IsAdapted.Value ? 1 : 0);
        }
        _count++;
    }

    /// <summary>Returns the canonical fingerprint only after the complete sealed history.</summary>
    public string Finish()
    {
        if (_finished || _count != _expectedCount)
            throw new InvalidOperationException("Owner history does not reach its sealed event count.");
        _finished = true;
        string fingerprint = DomainSharedProjectionRebuildFingerprint.Format(_hash.GetHashAndReset());
        return _legacyVersion ? fingerprint : "v2:" + fingerprint[3..];
    }

    private void AppendNullableInt64(long? value)
    {
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, value is null ? 0 : 1);
        if (value is not null)
            DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, value.Value);
    }

    private void AppendNullableBytes(byte[]? value)
    {
        DomainSharedProjectionRebuildFingerprint.AppendInt64(_hash, value is null ? 0 : 1);
        if (value is not null)
            DomainSharedProjectionRebuildFingerprint.AppendBytes(_hash, value);
    }

    /// <inheritdoc/>
    public void Dispose() => _hash.Dispose();
}
