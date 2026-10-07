using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Admits and privately owns one bounded legacy replay array before any domain callback.</summary>
/// <remarks>This local admission establishes no authenticated source proof or bound on a domain's working state graph.</remarks>
internal sealed class LegacyReplayInput : IDisposable
{
    private const int MaximumEvents = 100_000;
    private const long MaximumReadableBytes = 64L * 1024 * 1024;
    private const long MaximumAccountedBytes = 256L * 1024 * 1024;
    private const int PerEventCharge = 8192;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReplayEventEnvelope[] _events;
    private readonly IReadOnlyList<ReplayEventEnvelope> _eventView;

    private LegacyReplayInput(ReplayEventEnvelope[] events, AggregateReconstructionResult? refusal = null)
    {
        _events = events;
        _eventView = Array.AsReadOnly(events);
        Refusal = refusal;
    }

    /// <summary>Gets private eligible events in stream order after complete admission.</summary>
    internal IReadOnlyList<ReplayEventEnvelope> Events => _eventView;

    /// <summary>Gets the typed refusal, with no partial state or timeline.</summary>
    internal AggregateReconstructionResult? Refusal { get; }

    /// <summary>Checks complete source capacity before sorting or allocating private payload copies.</summary>
    internal static LegacyReplayInput Capture(AggregateReconstructionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ReplayEventEnvelope> source = request.Events;
        if (source is null)
        {
            return Refuse("ReplayScalarInvalid", AggregateReconstructionErrorCategory.Conflict);
        }

        int count = source.Count;
        cancellationToken.ThrowIfCancellationRequested();
        if (count is < 0 or > MaximumEvents || count > MaximumAccountedBytes / PerEventCharge)
        {
            return Refuse("LegacyArrayLimit");
        }

        // Capture all caller references before a converter, state constructor or Apply can run.
        var snapshot = new ReplayEventEnvelope[count];
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReplayEventEnvelope? item = source[index];
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null)
            {
                return Refuse("ReplayScalarInvalid", AggregateReconstructionErrorCategory.Conflict);
            }

            snapshot[index] = item;
        }

        long readableBytes = 0;
        long copyBytes = 0;
        long accountedBytes = (long)count * PerEventCharge;
        int eligibleCount = 0;
        foreach (ReplayEventEnvelope item in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long payloadBytes = item.Payload?.LongLength ?? 0;
            long metadataBytes;
            try
            {
                metadataBytes = EncodedMetadataLength(item, cancellationToken);
            }
            catch (EncoderFallbackException)
            {
                return Refuse("ReplayScalarInvalid", AggregateReconstructionErrorCategory.Conflict);
            }

            if (metadataBytes > 512 * 1024)
            {
                return Refuse("MetadataLimit");
            }

            long metadataCapacity = 128;
            foreach (string? value in new[] { item.EventTypeName, item.SerializationFormat,
                item.MessageId, item.CorrelationId, item.CausationId, item.StoredEventContractType,
                item.StoredSerializationFormat, item.StoredEventTypeName, item.RegistryFingerprint,
                item.EffectiveEventContractType, item.EffectiveSerializationFormat })
            {
                try
                {
                    metadataCapacity = checked(metadataCapacity + 2L * (value?.Length ?? 0)
                        + StrictUtf8.GetByteCount(value ?? string.Empty));
                }
                catch (EncoderFallbackException)
                {
                    return Refuse("ReplayScalarInvalid", AggregateReconstructionErrorCategory.Conflict);
                }
            }

            if (payloadBytes > MaximumReadableBytes - readableBytes)
            {
                return Refuse("LegacyArrayLimit");
            }

            readableBytes += payloadBytes;
            // Both encoded source metadata and retained managed strings belong to admission.
            // The larger conservative charge covers their shared logical value without claiming a raw-wire capture.
            accountedBytes = checked(accountedBytes + payloadBytes + Math.Max(metadataBytes, metadataCapacity)
                + (item.EffectivePayload?.LongLength ?? 0) + (item.StoredDigest?.LongLength ?? 0));
            if (item.SequenceNumber <= request.UpToSequence)
            {
                eligibleCount++;
                copyBytes += payloadBytes;
            }

            if (accountedBytes > MaximumAccountedBytes - copyBytes)
            {
                return Refuse("LegacyArrayLimit");
            }
        }

        var owned = new ReplayEventEnvelope[eligibleCount];
        int captured = 0;
        try
        {
            foreach (ReplayEventEnvelope item in snapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.SequenceNumber <= request.UpToSequence)
                {
                    byte[] payload = item.Payload?.ToArray() ?? [];
                    try
                    {
                        owned[captured] = item with { Payload = payload };
                        captured++;
                    }
                    catch
                    {
                        CryptographicOperations.ZeroMemory(payload);
                        throw;
                    }
                }
            }

            Array.Sort(owned, static (left, right) => left.SequenceNumber.CompareTo(right.SequenceNumber));
            cancellationToken.ThrowIfCancellationRequested();
            return new LegacyReplayInput(owned);
        }
        catch
        {
            for (int index = 0; index < captured; index++)
            {
                CryptographicOperations.ZeroMemory(owned[index].Payload);
            }

            throw;
        }
    }

    /// <summary>Clears privately copied payloads on success, refusal, failure and cancellation.</summary>
    public void Dispose()
    {
        foreach (ReplayEventEnvelope item in _events)
        {
            CryptographicOperations.ZeroMemory(item.Payload);
        }
    }

    /// <summary>Refuses an incomplete or ambiguous selected prefix before replay ownership callbacks.</summary>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The existing legacy sequence refusal, or null for a contiguous selected prefix.</returns>
    internal AggregateReconstructionResult? ValidatePrefix(CancellationToken cancellationToken)
    {
        IReadOnlyList<ReplayEventEnvelope> events = _eventView;
        cancellationToken.ThrowIfCancellationRequested();
        if (events.Count > 0 && events[0].SequenceNumber != 1)
        {
            return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Unexpected,
                "Missing stream sequence 1 detected during replay; reconstruction cannot skip events.",
                failedSequenceNumber: 1, failedEventType: string.Empty);
        }

        for (int index = 1; index < events.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReplayEventEnvelope current = events[index];
            long priorSequence = events[index - 1].SequenceNumber;
            if (current.SequenceNumber == priorSequence)
            {
                return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Unexpected,
                    string.Format(CultureInfo.InvariantCulture,
                        "Duplicate stream sequence {0} detected during replay; reconstruction cannot disambiguate ordering.", current.SequenceNumber),
                    failedSequenceNumber: current.SequenceNumber, failedEventType: current.EventTypeName);
            }

            // The sorted non-duplicate successor proves the prior is below long.MaxValue.
            long expectedSequence = priorSequence + 1;
            if (current.SequenceNumber != expectedSequence)
            {
                return AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Unexpected,
                    string.Format(CultureInfo.InvariantCulture,
                        "Missing stream sequence {0} detected during replay; reconstruction cannot skip events.", expectedSequence),
                    failedSequenceNumber: expectedSequence, failedEventType: string.Empty);
            }
        }

        return null;
    }

    private static long EncodedMetadataLength(ReplayEventEnvelope item, CancellationToken token)
    {
        long encoded = 2;
        int members = 0;
        void Member(string name, long valueLength)
            => encoded = checked(encoded + (members++ == 0 ? 0 : 1) + name.Length + 3 + valueLength);
        Member("sequenceNumber", NumberLength(item.SequenceNumber));
        Member("eventTypeName", StringLength(item.EventTypeName, token));
        Member("payload", 2); // Base64 payload content belongs to the readable-payload budget.
        Member("serializationFormat", StringLength(item.SerializationFormat, token));
        Member("metadataVersion", NumberLength(item.MetadataVersion));
        Member("messageId", StringLength(item.MessageId, token));
        Member("correlationId", StringLength(item.CorrelationId, token));
        Member("causationId", StringLength(item.CausationId, token));
        foreach ((string name, string? value) in new[]
        {
            ("storedEventContractType", item.StoredEventContractType),
            ("storedSerializationFormat", item.StoredSerializationFormat),
            ("storedEventTypeName", item.StoredEventTypeName),
            ("registryFingerprint", item.RegistryFingerprint),
            ("effectiveEventContractType", item.EffectiveEventContractType),
            ("effectiveSerializationFormat", item.EffectiveSerializationFormat),
        })
        {
            if (value is not null) { Member(name, StringLength(value, token)); }
        }

        if (item.StoredPayloadVersion is int storedVersion) { Member("storedPayloadVersion", NumberLength(storedVersion)); }
        if (item.EffectivePayloadVersion is int effectiveVersion) { Member("effectivePayloadVersion", NumberLength(effectiveVersion)); }
        if (item.IsAdapted is bool adapted) { Member("isAdapted", adapted ? 4 : 5); }
        if (item.StoredDigest is byte[] digest) { Member("storedDigest", 2 + 4L * ((digest.LongLength + 2) / 3)); }
        if (item.EffectivePayload is not null) { Member("effectivePayload", 2); }
        return encoded;
    }

    private static long StringLength(string? value, CancellationToken token)
    {
        if (value is null) { return 4; }
        long length = 2;
        for (int offset = 0; offset < value.Length;)
        {
            token.ThrowIfCancellationRequested();
            if (Rune.DecodeFromUtf16(value.AsSpan(offset), out Rune rune, out int consumed) != OperationStatus.Done)
            {
                throw new EncoderFallbackException("Replay metadata contains invalid Unicode.");
            }

            offset += consumed;
            length += rune.Value switch
            {
                8 or 9 or 10 or 12 or 13 or 92 => 2,
                _ => JavaScriptEncoder.Default.WillEncode(rune.Value) ? 6L * rune.Utf16SequenceLength : rune.Utf8SequenceLength,
            };
            if (length > 512 * 1024) { return length; }
        }

        return length;
    }

    private static int NumberLength(long value)
    {
        Span<char> digits = stackalloc char[20];
        _ = value.TryFormat(digits, out int written, provider: CultureInfo.InvariantCulture);
        return written;
    }

    private static LegacyReplayInput Refuse(string reason, AggregateReconstructionErrorCategory category = AggregateReconstructionErrorCategory.Limit)
        => new([], AggregateReconstructionResult.Failed(category, "Legacy replay input could not be admitted.") with { ReasonCode = reason });
}
