using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Handlers;

/// <summary>Bounds and clears private contract-envelope copies used by legacy command reconstruction.</summary>
/// <remarks>
/// Nested envelope sequences share one count, readable-payload and live-array admission budget.
/// This local owner grants no authenticated source authority or bound on typed state/event graphs.
/// JSON documents and base64 buffers are admitted before private materialization. Original
/// transport parsing and application converter/typed graph allocations remain unqualified.
/// Caller-owned typed snapshots are not cloned.
/// </remarks>
internal sealed class LegacyCommandReplayInput : IDisposable
{
    private const int MaximumEvents = 100_000;
    private const int PerEventCharge = 8192;
    private const int MaximumMetadataBytes = 512 * 1024;
    private const long MaximumReadableBytes = 64L * 1024 * 1024;
    private const long MaximumAccountedBytes = 256L * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly CancellationToken _cancellationToken;
    private readonly List<byte[]> _payloads = [];
    private readonly List<JsonDocument> _jsonDocuments = [];
    private long _accountedBytes;
    private long _readableBytes;
    private long _jsonDecodedBytesAvailable;
    private int _eventCount;
    private bool _disposed;

    /// <summary>Creates one admission owner for the complete command-state reconstruction.</summary>
    internal LegacyCommandReplayInput(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _cancellationToken = cancellationToken;
    }

    /// <summary>Refuses excessive wrapper recursion and charges empty wrappers before traversing them.</summary>
    internal void AdmitSnapshotWrapper(int depth)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        if (depth >= 64) { Refuse("LegacyArrayLimit"); }
        Account(256);
    }

    /// <summary>Captures references once, admits the whole sequence, then copies contract payloads.</summary>
    internal List<object?> CaptureEvents(IEnumerable events)
    {
        ArgumentNullException.ThrowIfNull(events);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        int? count = events switch
        {
            ICollection collection => collection.Count,
            IReadOnlyCollection<EventEnvelope> collection => collection.Count,
            _ => null,
        };
        if (count is int knownCount)
        {
            RequireCount(knownCount);
        }

        // A collection may report more entries than it actually yields. Charge its
        // requested list capacity independently of the per-yield envelope overhead.
        Account(checked(128L + (count ?? 0) * 2L * IntPtr.Size));
        var references = count is int capacity ? new List<object?>(capacity) : [];
        foreach (object? item in events)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            RequireCount(1);
            Account(PerEventCharge);
            _eventCount++;
            references.Add(item);
        }

        // All source references and capacities are admitted before private payload copying.
        // No payload converter, state constructor or Apply runs inside this owner.
        foreach (object? item in references)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (item is EventEnvelope envelope)
            {
                AdmitEnvelope(envelope);
            }
        }

        for (int index = 0; index < references.Count; index++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (references[index] is EventEnvelope envelope)
            {
                byte[] copy = envelope.Payload.ToArray();
                try
                {
                    _payloads.Add(copy);
                }
                catch
                {
                    CryptographicOperations.ZeroMemory(copy);
                    throw;
                }

                references[index] = new EventEnvelope(envelope.Metadata, copy, envelope.Extensions);
            }
            else if (references[index] is JsonElement json)
            {
                references[index] = CaptureJson(json, reserveEvents: true, eventRoot: true, countAlreadyAdmitted: true);
            }
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return references;
    }

    /// <summary>Admits the complete JSON source before privately copying or binding any of it.</summary>
    internal JsonElement CaptureJson(JsonElement source, bool reserveEvents,
        bool eventRoot = false, bool countAlreadyAdmitted = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        (int count, long readable, long documents) = LegacyCommandReplayJsonAdmission.Measure(source, _cancellationToken, eventRoot);
        int additionalCount = countAlreadyAdmitted ? 0 : count;
        RequireCount(additionalCount);
        if (readable > MaximumReadableBytes - _readableBytes) { Refuse("LegacyArrayLimit"); }
        long eventCapacity = checked(additionalCount * (long)PerEventCharge + 2 * readable);
        if (documents + eventCapacity > MaximumAccountedBytes - _accountedBytes) { Refuse("LegacyArrayLimit"); }
        // Only built-in fixed metadata/dictionary binding runs here. No event payload,
        // application converter, state constructor or Apply is materialized by this check.
        LegacyCommandReplayJsonAdmission.ValidateContractMetadata(source, AdmitEnvelope, _cancellationToken);
        if (documents + eventCapacity > MaximumAccountedBytes - _accountedBytes) { Refuse("LegacyArrayLimit"); }
        Account(documents);
        if (reserveEvents)
        {
            Account(eventCapacity);
            _eventCount += additionalCount;
            /* mutation: skip only JSON readable-byte accumulation */
        }

        _jsonDecodedBytesAvailable += readable;

        byte[] copy = JsonMarshal.GetRawUtf8Value(source).ToArray();
        RetainPayload(copy);
        JsonDocument document = JsonDocument.Parse((ReadOnlyMemory<byte>)copy, new JsonDocumentOptions
        {
            AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip,
        });
        try { _jsonDocuments.Add(document); }
        catch { document.Dispose(); throw; }
        _cancellationToken.ThrowIfCancellationRequested();
        return document.RootElement;
    }

    /// <summary>Retains an already admitted base64 decode until reconstruction ends.</summary>
    internal byte[] DecodePayload(JsonElement value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        long expectedLength = LegacyCommandReplayJsonAdmission.DecodedBase64Length(value, _cancellationToken);
        if (expectedLength > _jsonDecodedBytesAvailable) { Refuse("LegacyArrayLimit"); }
        _jsonDecodedBytesAvailable -= expectedLength;
        byte[] bytes = new byte[checked((int)expectedLength)];
        RetainPayload(bytes);
        LegacyCommandReplayJsonAdmission.DecodeBase64(value, bytes, _cancellationToken);
        _cancellationToken.ThrowIfCancellationRequested();
        return bytes;
    }

    /// <summary>Admits decoded JSON token-table capacity before parsing retained payload bytes.</summary>
    internal JsonDocument ParsePayload(byte[] bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        var reader = new Utf8JsonReader(bytes);
        while (reader.Read())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            // Cover document token-table capacity and resizing slack, in addition to
            // the source/private byte buffers admitted by envelope or JSON capture.
            Account(128);
        }

        _cancellationToken.ThrowIfCancellationRequested();
        JsonDocument document = JsonDocument.Parse((ReadOnlyMemory<byte>)bytes);
        try { _jsonDocuments.Add(document); }
        catch { document.Dispose(); throw; }
        _cancellationToken.ThrowIfCancellationRequested();
        return document;
    }

    /// <summary>Clears every privately retained payload on success, refusal, failure or cancellation.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        foreach (JsonDocument document in _jsonDocuments) { document.Dispose(); }
        _jsonDocuments.Clear();
        foreach (byte[] payload in _payloads)
        {
            CryptographicOperations.ZeroMemory(payload);
        }

        _payloads.Clear();
    }

    private void RetainPayload(byte[] bytes)
    {
        try { _payloads.Add(bytes); }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private void AdmitEnvelope(EventEnvelope envelope)
    {
        EventMetadata metadata = envelope.Metadata;
        long encoded = 2;
        long managed = 256;
        int members = 0;
        void Member(string name, long valueLength)
            => encoded = checked(encoded + (members++ == 0 ? 0 : 1) + name.Length + 3 + valueLength);
        void Text(string name, string? value)
        {
            Member(name, StringLength(value));
            managed = checked(managed + 2L * (value?.Length ?? 0) + StrictUtf8.GetByteCount(value ?? string.Empty));
        }

        try
        {
            Text("messageId", metadata.MessageId);
            Text("aggregateId", metadata.AggregateId);
            Text("aggregateType", metadata.AggregateType);
            Text("tenantId", metadata.TenantId);
            Text("domain", metadata.Domain);
            Member("sequenceNumber", NumberLength(metadata.SequenceNumber));
            Member("globalPosition", NumberLength(metadata.GlobalPosition));
            // The primitive timestamp has a fixed, bounded representation and no application converter.
            Member("timestamp", JsonSerializer.SerializeToUtf8Bytes(metadata.Timestamp).Length);
            Text("correlationId", metadata.CorrelationId);
            Text("causationId", metadata.CausationId);
            Text("userId", metadata.UserId);
            Text("domainServiceVersion", metadata.DomainServiceVersion);
            Text("eventTypeName", metadata.EventTypeName);
            Member("metadataVersion", NumberLength(metadata.MetadataVersion));
            Text("serializationFormat", metadata.SerializationFormat);
            if (metadata.EventContractType is not null) { Text("eventContractType", metadata.EventContractType); }
            if (metadata.PayloadVersion is int version) { Member("payloadVersion", NumberLength(version)); }

            long extensions = 2;
            int extensionCount = 0;
            foreach ((string key, string value) in envelope.Extensions)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                extensions = checked(extensions + (extensionCount++ == 0 ? 0 : 1) + StringLength(key) + 1 + StringLength(value));
                // Retained legacy extensions are not a new writer declaration. Preserve their
                // keys/values and conservatively charge both source and copied dictionary
                // buckets/entries, including capacity slack, before constructing the copy.
                managed = checked(managed + 256 + 2L * (key.Length + (value?.Length ?? 0))
                    + StrictUtf8.GetByteCount(key) + StrictUtf8.GetByteCount(value ?? string.Empty));
                if (encoded + extensions > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
            }

            // Account the enclosing envelope with an empty base64 payload, as on the replay lane.
            encoded = checked(encoded + extensions + 40); // {"metadata":...,"payload":"","extensions":...}
            if (encoded > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
        }
        catch (EncoderFallbackException)
        {
            Refuse("ReplayScalarInvalid");
        }

        long bytes = envelope.Payload.LongLength;
        if (bytes > MaximumReadableBytes - _readableBytes) { Refuse("LegacyArrayLimit"); }
        _readableBytes += bytes;
        // Keep caller/source payload, private copy, encoded metadata and retained managed strings
        // under the independent legacy-array ceiling. The per-event charge covers container capacity.
        Account(checked(2 * bytes + Math.Max(encoded, managed)));
    }

    private void RequireCount(int count)
    {
        if (count < 0 || count > MaximumEvents - _eventCount
            || count > (MaximumAccountedBytes - _accountedBytes) / PerEventCharge)
        {
            Refuse("LegacyArrayLimit");
        }
    }

    private void Account(long capacity)
    {
        if (capacity < 0 || capacity > MaximumAccountedBytes - _accountedBytes) { Refuse("LegacyArrayLimit"); }
        _accountedBytes += capacity;
    }

    private long StringLength(string? value)
    {
        if (value is null) { return 4; }
        long length = 2;
        for (int offset = 0; offset < value.Length;)
        {
            _cancellationToken.ThrowIfCancellationRequested();
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
            if (length > MaximumMetadataBytes) { Refuse("MetadataLimit"); }
        }

        return length;
    }

    private static int NumberLength(long value)
    {
        Span<char> digits = stackalloc char[20];
        _ = value.TryFormat(digits, out int written, provider: CultureInfo.InvariantCulture);
        return written;
    }

    private static void Refuse(string reason)
        => throw new InvalidOperationException($"{reason}: legacy command-state envelope admission failed.");
}
