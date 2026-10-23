using System.Globalization;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>A deterministic CAS fault fixture only; it is never PF-02 evidence.</summary>
internal sealed class MemoryPayloadProtectionStateStore(TimeProvider? timeProvider = null) : IPayloadProtectionStateStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (byte[] Bytes, string ETag, DateTimeOffset? Expires)> _values = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private long _version;
    /// <summary>Gets or sets a one-shot precommit fault hook for CAS races.</summary>
    internal Action? BeforeCommit { get; set; }
    /// <summary>Gets or sets an ambiguous acknowledgement fault after atomic persistence.</summary>
    internal bool LoseNextAcknowledgement { get; set; }
    /// <summary>Gets the number of atomic requests.</summary>
    internal int Commits { get; private set; }

    /// <summary>Reads one closed exact record with a copied byte array.</summary>
    public ValueTask<PayloadProtectionStateValue<T>?> ReadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Expire();
            if (!_values.TryGetValue(key, out var value)) { return ValueTask.FromResult<PayloadProtectionStateValue<T>?>(null); }
            T record = PayloadProtectionStateCodec.Decode<T>(value.Bytes);
            PayloadProtectionStateCodec.KeyFor(record).ShouldBe(key);
            return ValueTask.FromResult<PayloadProtectionStateValue<T>?>(new(record, value.ETag, value.Bytes.ToArray()));
        }
    }

    /// <summary>Compares every condition before changing any persisted fixture value.</summary>
    public ValueTask<PayloadStateCommitResult> CommitAsync(IReadOnlyList<PayloadProtectionStateMutation> mutations, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Action? hook = BeforeCommit;
        BeforeCommit = null;
        hook?.Invoke();
        lock (_gate)
        {
            Expire();
            Commits++;
            mutations.Select(m => m.Key).Distinct(StringComparer.Ordinal).Count().ShouldBe(mutations.Count);
            foreach (PayloadProtectionStateMutation mutation in mutations)
            {
                bool exists = _values.TryGetValue(mutation.Key, out var value);
                if (mutation.ExpectedETag is null ? exists : !exists || value.ETag != mutation.ExpectedETag) { return ValueTask.FromResult(PayloadStateCommitResult.Conflict); }
            }
            foreach (PayloadProtectionStateMutation mutation in mutations)
            {
                if (mutation.Bytes is null) { _values.Remove(mutation.Key); }
                else { _values[mutation.Key] = (mutation.Bytes.ToArray(), (++_version).ToString(CultureInfo.InvariantCulture),
                    mutation.TtlSeconds.HasValue ? _clock.GetUtcNow().AddSeconds(mutation.TtlSeconds.Value) : null); }
            }
            bool unknown = LoseNextAcknowledgement;
            LoseNextAcknowledgement = false;
            return ValueTask.FromResult(unknown ? PayloadStateCommitResult.Unknown : PayloadStateCommitResult.Committed);
        }
    }
    private void Expire()
    {
        foreach (string key in _values.Where(p => p.Value.Expires <= _clock.GetUtcNow()).Select(p => p.Key).ToArray()) { _values.Remove(key); }
    }
}
