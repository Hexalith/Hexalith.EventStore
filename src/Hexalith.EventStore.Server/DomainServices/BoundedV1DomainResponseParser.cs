using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Server.DomainServices;

/// <summary>Admits implicit V1 response shape, recursive duplicate names and raw metadata before DTO construction.</summary>
/// <remarks>Registered alias/schema/identity authority and operation-wide ownership remain separate admission requirements.</remarks>
internal sealed class BoundedV1DomainResponseParser : IDisposable
{
    private readonly DomainResponseBufferBudget _budget = new();
    private readonly IncrementalDomainJsonTokenReader _reader;
    private readonly CancellationToken _token;
    private readonly int _maximumPayload;
    private readonly int _maximumEvents;
    private readonly List<byte[]> _payloads = [];
    private readonly List<long> _retainedStrings = [];
    private readonly List<PendingV1WireEvent> _events = [];
    private int _nodes;
    private long? _eventStart;
    private long _payloadScalarBytes;
    private bool _detached;
    private bool _started;
    private bool _disposed;
    private long _eventCharges;

    /// <summary>Uses a bounded transport stream and the default 1 MiB readable ceiling unless explicitly supplied.</summary>
    internal BoundedV1DomainResponseParser(Stream source, CancellationToken token, int maximumPayload = 1024 * 1024, int maximumEvents = 1000)
    {
        if (maximumPayload is < 0 or > 64 * 1024 * 1024) { throw new ArgumentOutOfRangeException(nameof(maximumPayload)); }
        if (maximumEvents is < 0 or > 1000) { throw new ArgumentOutOfRangeException(nameof(maximumEvents)); }
        _maximumPayload = maximumPayload;
        _maximumEvents = maximumEvents;
        _token = token;
        _reader = new IncrementalDomainJsonTokenReader(source, _budget, token);
    }

    /// <summary>Gets private live capacity for bounded failure/disposal evidence.</summary>
    internal long LiveBytes => _budget.LiveBytes;

    /// <summary>Constructs no wire result until the root closes, all fields/events validate and no trailing token exists.</summary>
    internal async Task<DomainServiceWireResult> ParseAsync()
    {
        if (_started) { throw new InvalidOperationException("A response parser is one-shot."); }
        _started = true;
        bool rejection = false;
        bool eventsPresent = false;
        string? resultPayload = null;
        await NextAsync().ConfigureAwait(false);
        Require(JsonTokenType.StartObject);
        await ObjectAsync(async name =>
        {
            if (name.Equals("writerMode", StringComparison.OrdinalIgnoreCase)
                || name.Equals("registryFingerprint", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CapabilityMismatch: an implicit V1 response must omit both echo fields.");
            }
            if (name.Equals("events", StringComparison.OrdinalIgnoreCase))
            {
                eventsPresent = true;
                await NextAsync().ConfigureAwait(false);
                Require(JsonTokenType.StartArray);
                while (await NextAsync().ConfigureAwait(false) is not JsonTokenType.EndArray)
                {
                    CountNode();
                    if (_events.Count == _maximumEvents) { throw new InvalidOperationException("ResultLimit: a V1 response exceeds the selected event count."); }
                    Require(JsonTokenType.StartObject);
                    _budget.Reserve(256);
                    _eventCharges += 256;
                    _eventStart = _reader.TokenStart;
                    _payloadScalarBytes = 0;
                    var item = new PendingV1WireEvent();
                    await ObjectAsync(name => EventFieldAsync(item, name)).ConfigureAwait(false);
                    CheckMetadata();
                    _eventStart = null;
                    if (string.IsNullOrEmpty(item.TypeName) || item.Payload is null || string.IsNullOrEmpty(item.Format)
                        || item.ContractPresent != item.VersionPresent || item.MetadataVersion is not (null or 1))
                    {
                        throw new InvalidOperationException("MalformedMetadata: missing/contradictory V1 event fields.");
                    }
                    _events.Add(item);
                }
            }
            else if (name.Equals("isRejection", StringComparison.OrdinalIgnoreCase))
            {
                JsonTokenType kind = await NextAsync().ConfigureAwait(false);
                if (kind is not (JsonTokenType.True or JsonTokenType.False)) { throw new JsonException("isRejection must be boolean."); }
                rejection = kind == JsonTokenType.True;
            }
            else if (name.Equals("resultPayload", StringComparison.OrdinalIgnoreCase))
            {
                JsonTokenType kind = await NextAsync().ConfigureAwait(false);
                if (kind == JsonTokenType.String) { resultPayload = DecodeRetained(); }
                else if (kind != JsonTokenType.Null) { throw new JsonException("resultPayload must be string or null."); }
            }
            else
            {
                await NextAsync().ConfigureAwait(false);
                await SkipAsync().ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
        if (!eventsPresent) { throw new JsonException("events must be one array."); }
        if (await _reader.ReadAsync().ConfigureAwait(false)) { throw new JsonException("Trailing response token."); }
        _token.ThrowIfCancellationRequested();
        DomainServiceWireEvent[] events = _events.Select(static item => new DomainServiceWireEvent(item.TypeName!, item.Payload!, item.Format)
        { MetadataVersion = item.MetadataVersion }).ToArray();
        var result = new DomainServiceWireResult(rejection, events, resultPayload);
        // Transfer the already charged decoded arrays; there is no second typed payload copy.
        _detached = true;
        return result;
    }

    /// <summary>Clears every detached candidate payload on later event/root failure.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        _reader.Dispose();
        if (!_detached)
        {
            foreach (byte[] payload in _payloads)
            {
                CryptographicOperations.ZeroMemory(payload);
                _budget.Release(payload.Length);
            }
        }
        if (!_detached) { foreach (long charge in _retainedStrings) { _budget.Release(charge); } }
        if (!_detached) { _budget.Release(_eventCharges); }
        _payloads.Clear();
        _retainedStrings.Clear();
        _events.Clear();
    }

    /// <summary>Checks exact raw pair presence and V1 values while retaining only bounded admitted fields.</summary>
    private async Task EventFieldAsync(PendingV1WireEvent item, string name)
    {
        if (name.Equals("payload", StringComparison.OrdinalIgnoreCase))
        {
            long maximumScalar = checked(4L * ((_maximumPayload + 2L) / 3) * 6 + 2);
            using var admission = new IncrementalDomainBase64Admission(_budget, _maximumPayload);
            if (!await _reader.ReadAsync(maximumScalar, admission, _eventStart!.Value + _payloadScalarBytes + 512 * 1024L).ConfigureAwait(false)) { throw new JsonException("Missing payload scalar."); }
            _payloadScalarBytes = _reader.RawValue.Length + 2L;
            item.Payload = admission.Complete();
            _payloads.Add(item.Payload);
            return;
        }
        JsonTokenType kind = await NextAsync().ConfigureAwait(false);
        if (name.Equals("eventTypeName", StringComparison.OrdinalIgnoreCase))
        {
            Require(JsonTokenType.String); item.TypeName = DecodeRetained();
        }
        else if (name.Equals("serializationFormat", StringComparison.OrdinalIgnoreCase))
        {
            Require(JsonTokenType.String); item.Format = DecodeRetained();
        }
        else if (name.Equals("metadataVersion", StringComparison.OrdinalIgnoreCase))
        {
            if (kind == JsonTokenType.Number && _reader.RawValue.SequenceEqual("1"u8)) { item.MetadataVersion = 1; }
            else if (kind == JsonTokenType.Number && _reader.RawValue.SequenceEqual("2"u8)) { throw new InvalidOperationException("CapabilityMismatch: unsolicited metadata V2 response."); }
            else if (kind != JsonTokenType.Null) { throw new InvalidOperationException("MalformedMetadata: implicit V1 metadataVersion must be null or one."); }
        }
        else if (name.Equals("eventContractType", StringComparison.OrdinalIgnoreCase))
        {
            item.ContractPresent = true;
            if (kind != JsonTokenType.Null) { throw new InvalidOperationException("CapabilityMismatch: unsolicited canonical pair."); }
        }
        else if (name.Equals("payloadVersion", StringComparison.OrdinalIgnoreCase))
        {
            item.VersionPresent = true;
            if (kind != JsonTokenType.Null) { throw new InvalidOperationException("CapabilityMismatch: unsolicited canonical pair."); }
        }
        else { await SkipAsync().ConfigureAwait(false); }
        CheckMetadata();
    }

    /// <summary>Checks recursive .NET ordinal-ignore-case duplicates before any known-field interpretation.</summary>
    private async Task ObjectAsync(Func<string, Task> field)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var charges = new List<long>();
        _budget.Reserve(256);
        try
        {
            while (await NextAsync().ConfigureAwait(false) is not JsonTokenType.EndObject)
            {
                Require(JsonTokenType.PropertyName);
                CountNode();
                _budget.Reserve(128);
                long nameCharge = 128;
                try
                {
                    (string name, long charge) = _reader.DecodeString();
                    nameCharge += charge;
                    charges.Add(nameCharge);
                    nameCharge = 0;
                    if (!names.Add(name)) { throw new JsonException("Duplicate ordinal/case-insensitive response property."); }
                    await field(name).ConfigureAwait(false);
                }
                finally { if (nameCharge > 0) { _budget.Release(nameCharge); } }
            }
        }
        finally
        {
            names.Clear();
            foreach (long charge in charges) { _budget.Release(charge); }
            charges.Clear();
            _budget.Release(256);
        }
    }

    /// <summary>Skips unknown V1 values with the same Unicode, duplicate, node and depth admission.</summary>
    private async Task SkipAsync()
    {
        if (_reader.TokenType == JsonTokenType.StartObject)
        {
            await ObjectAsync(async _ => { await NextAsync().ConfigureAwait(false); await SkipAsync().ConfigureAwait(false); }).ConfigureAwait(false);
        }
        else if (_reader.TokenType == JsonTokenType.StartArray)
        {
            while (await NextAsync().ConfigureAwait(false) is not JsonTokenType.EndArray)
            {
                CountNode();
                await SkipAsync().ConfigureAwait(false);
            }
        }
        else if (_reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject or JsonTokenType.PropertyName)
        {
            throw new JsonException("Unexpected response value shape.");
        }
    }

    /// <summary>Enforces per-event metadata and scalar capacity before decoded materialization or token growth.</summary>
    private async ValueTask<JsonTokenType> NextAsync()
    {
        long maximum = _eventStart.HasValue ? 512 * 1024L - (_reader.BytesConsumed - _eventStart.Value - _payloadScalarBytes) : long.MaxValue;
        if (maximum < 0) { throw new InvalidOperationException("MetadataLimit: encoded event metadata exceeds 512 KiB."); }
        try
        {
            if (!await _reader.ReadAsync(maximum, maximumConsumedBytes: _eventStart.HasValue ? _eventStart.Value + _payloadScalarBytes + 512 * 1024L : long.MaxValue).ConfigureAwait(false)) { throw new JsonException("Incomplete response."); }
        }
        catch (InvalidOperationException exception) when (_eventStart.HasValue && exception.Message.StartsWith("PayloadLimit:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("MetadataLimit: encoded event metadata exceeds 512 KiB.", exception);
        }
        return _reader.TokenType;
    }

    /// <summary>Retains charged strings only after metadata/decoded capacity admission.</summary>
    private string DecodeRetained()
    {
        CheckMetadata();
        (string value, long charge) = _reader.DecodeString();
        _retainedStrings.Add(charge);
        return value;
    }

    /// <summary>Checks full encoded event framing excluding only the payload scalar.</summary>
    private void CheckMetadata()
    {
        if (_eventStart.HasValue && _reader.BytesConsumed - _eventStart.Value - _payloadScalarBytes > 512 * 1024)
        {
            throw new InvalidOperationException("MetadataLimit: encoded event metadata exceeds 512 KiB.");
        }
    }

    /// <summary>Checks the approved complete response node count without allocating a JSON tree.</summary>
    private void CountNode()
    {
        if (++_nodes > 1_000_000) { throw new InvalidOperationException("ResultLimit: JSON response exceeds 1,000,000 nodes."); }
    }

    /// <summary>Rejects an unexpected raw token before typed binding.</summary>
    private void Require(JsonTokenType kind)
    {
        if (_reader.TokenType != kind) { throw new JsonException("Unexpected response shape."); }
    }
}
