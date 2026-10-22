using System.Text;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Retains immutable source/route hashes and exact packed identities, with bounded prefix checkpoints.</summary>
internal sealed class ProjectionSourceAdmittedDigests
{
    private const int HashBytes = 32;
    private const int EntryBytes = 2 * HashBytes;
    private const int PrefixStride = 32;
    private readonly long _baseSequence;
    private readonly bool _positionSensitive;
    private readonly string _baseFingerprint;
    private byte[] _messageBytes;
    private readonly int[] _messageOffsets;
    private Dictionary<int, string>? _nonAscii;
    private readonly byte[] _hashes;
    private readonly byte[] _prefixCheckpoints;
    private readonly ProjectionSourceAdmittedDigestList<ProjectionSourceAdmittedEvent> _sourceEvents;
    private readonly ProjectionSourceAdmittedDigestList<ProjectionDeliveryEventDigest> _routeEvents;
    private string? _finalPrefix;
    private int _count, _messageBytesWritten;
    private bool _sealed;

    /// <summary>Allocates bounded authority for the frozen suffix without retaining per-event strings or redundant prefixes.</summary>
    internal ProjectionSourceAdmittedDigests(long baseSequence, long headSequence, bool positionSensitive, string baseFingerprint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(headSequence, baseSequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(headSequence, 10_000);
        _baseSequence = baseSequence;
        _positionSensitive = positionSensitive;
        _baseFingerprint = baseFingerprint;
        int length = checked((int)(headSequence - baseSequence));
        _messageBytes = new byte[checked(length * 36)];
        _messageOffsets = new int[length + 1];
        _hashes = new byte[checked(length * EntryBytes)];
        _prefixCheckpoints = new byte[checked(length / PrefixStride * HashBytes)];
        _sourceEvents = new(this, static (table, index) => table.SourceEventAt(index));
        _routeEvents = new(this, static (table, index) => table.RouteEventAt(index), static table => table.ReadRouteEvents());
    }

    /// <summary>Gets the exact number of admitted sequences.</summary>
    public int Count => _count;

    /// <summary>Gets original source tuples without retaining historical records or strings.</summary>
    internal IReadOnlyList<ProjectionSourceAdmittedEvent> SourceEvents => _sourceEvents;

    /// <summary>Gets unchanged v1/v2 route fingerprints through an immutable streaming view.</summary>
    internal IReadOnlyList<ProjectionDeliveryEventDigest> RouteEvents => _routeEvents;

    /// <summary>Appends one verified contiguous event before publishing either view.</summary>
    internal void Add(long sequence, string messageId, string sourceHash, string eventFingerprint, string prefixFingerprint)
    {
        if (_sealed || _count == _messageOffsets.Length - 1 || sequence != _baseSequence + _count + 1)
            throw new InvalidOperationException("source_conflict");
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        byte[] source = Convert.FromHexString(sourceHash), route = DecodeRoute(eventFingerprint);
        if (source.Length != HashBytes || Convert.ToHexStringLower(source) != sourceHash)
            throw new InvalidOperationException("source_conflict");
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits);
        if (ProjectionDeliveryFingerprint.Extend(_finalPrefix ?? _baseFingerprint, eventFingerprint) != prefixFingerprint)
            throw new InvalidOperationException("source_conflict");
        source.CopyTo(_hashes, _count * EntryBytes);
        route.CopyTo(_hashes, _count * EntryBytes + HashBytes);
        bool ascii = true;
        foreach (char value in messageId) if (value > 127) { ascii = false; break; }
        if (ascii)
        {
            int required = checked(_messageBytesWritten + messageId.Length);
            if (required > _messageBytes.Length) Array.Resize(ref _messageBytes, Math.Max(required, checked(_messageBytes.Length * 2)));
            _messageBytesWritten += Encoding.ASCII.GetBytes(messageId, _messageBytes.AsSpan(_messageBytesWritten));
        }
        else (_nonAscii ??= []).Add(_count, messageId);
        _messageOffsets[_count + 1] = _messageBytesWritten;
        if ((_count + 1) % PrefixStride == 0)
            DecodeRoute(prefixFingerprint).CopyTo(_prefixCheckpoints, _count / PrefixStride * HashBytes);
        _finalPrefix = prefixFingerprint;
        _count++;
    }

    /// <summary>Publishes only a complete frozen table.</summary>
    internal void Seal()
    {
        if (_count != _messageOffsets.Length - 1) throw new InvalidOperationException("delivery_gap");
        _sealed = true;
    }

    /// <summary>Matches a verification tuple; its caller separately authenticates the original base and full cumulative prefix.</summary>
    internal bool MatchesEvent(long sequence, string messageId, string sourceHash, string eventFingerprint)
    {
        long offset = sequence - _baseSequence - 1;
        if (!_sealed || offset < 0 || offset >= _count) return false;
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits);
        int index = (int)offset;
        return MessageAt(index) == messageId
            && _hashes.AsSpan(index * EntryBytes, HashBytes).SequenceEqual(Convert.FromHexString(sourceHash))
            && _hashes.AsSpan(index * EntryBytes + HashBytes, HashBytes).SequenceEqual(DecodeRoute(eventFingerprint));
    }

    /// <summary>Provides exact random tuple comparison, including its original cumulative prefix.</summary>
    internal bool Matches(long sequence, string messageId, string sourceHash, string eventFingerprint, string prefixFingerprint)
        => MatchesEvent(sequence, messageId, sourceHash, eventFingerprint)
            && PrefixAt(checked((int)(sequence - _baseSequence - 1))) == prefixFingerprint;

    private ProjectionSourceAdmittedEvent SourceEventAt(int index)
    {
        RequireIndex(index);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits);
        return new(_baseSequence + index + 1, MessageAt(index), Convert.ToHexStringLower(_hashes.AsSpan(index * EntryBytes, HashBytes)),
            EncodeRoute(_hashes.AsSpan(index * EntryBytes + HashBytes, HashBytes)));
    }

    private ProjectionDeliveryEventDigest RouteEventAt(int index)
    {
        RequireIndex(index);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits);
        return new(_baseSequence + index + 1, MessageAt(index),
            EncodeRoute(_hashes.AsSpan(index * EntryBytes + HashBytes, HashBytes)), PrefixAt(index));
    }

    private IEnumerable<ProjectionDeliveryEventDigest> ReadRouteEvents()
    {
        if (!_sealed) throw new InvalidOperationException("source_conflict");
        string prefix = _baseFingerprint;
        for (int index = 0; index < _count; index++)
        {
            string fingerprint = EncodeRoute(_hashes.AsSpan(index * EntryBytes + HashBytes, HashBytes));
            prefix = ProjectionDeliveryFingerprint.Extend(prefix, fingerprint);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits, 2);
            yield return new(_baseSequence + index + 1, MessageAt(index), fingerprint, prefix);
        }
    }

    private string PrefixAt(int index)
    {
        if (index == _count - 1) return _finalPrefix!;
        int block = index / PrefixStride;
        string prefix = block == 0 ? _baseFingerprint : EncodeRoute(_prefixCheckpoints.AsSpan((block - 1) * HashBytes, HashBytes));
        for (int cursor = block * PrefixStride; cursor <= index; cursor++)
        {
            prefix = ProjectionDeliveryFingerprint.Extend(prefix, EncodeRoute(_hashes.AsSpan(cursor * EntryBytes + HashBytes, HashBytes)));
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofVisits);
        }
        return prefix;
    }

    private string MessageAt(int index) => _nonAscii is not null && _nonAscii.TryGetValue(index, out string? original)
        ? original : Encoding.ASCII.GetString(_messageBytes.AsSpan(_messageOffsets[index], _messageOffsets[index + 1] - _messageOffsets[index]));

    private byte[] DecodeRoute(string value)
    {
        if (!value.StartsWith(_positionSensitive ? "v2:" : "v1:", StringComparison.Ordinal)) throw new InvalidOperationException("source_conflict");
        byte[] bytes = Convert.FromBase64String(value[3..].Replace('-', '+').Replace('_', '/') + "=");
        if (bytes.Length != HashBytes || EncodeRoute(bytes) != value) throw new InvalidOperationException("source_conflict");
        return bytes;
    }

    private string EncodeRoute(ReadOnlySpan<byte> bytes)
        => (_positionSensitive ? "v2:" : "v1:") + Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private void RequireIndex(int index)
    {
        if (!_sealed) throw new InvalidOperationException("source_conflict");
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
    }
}
