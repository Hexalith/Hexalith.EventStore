using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>Records every conditional mutation and whether the backing store accepted it.</summary>
internal sealed class ConditionalWriteRecordingBatchStateAccessor(IReadModelBatchStateAccessor inner) : IReadModelBatchStateAccessor
{
    private readonly List<(string Key, string ExpectedETag, bool Accepted)> _writes = [];
    private readonly List<(string Key, bool Accepted)> _deletes = [];

    internal IReadOnlyList<(string Key, string ExpectedETag, bool Accepted)> Writes => _writes;

    internal IReadOnlyList<(string Key, bool Accepted)> Deletes => _deletes;

    internal IReadOnlyList<string> RejectedMutations => [.. _writes.Where(static write => !write.Accepted).Select(static write => "write:" + write.Key),
        .. _deletes.Where(static delete => !delete.Accepted).Select(static delete => "delete:" + delete.Key)];

    internal Func<string, ReadOnlyMemory<byte>, CancellationToken, Task>? BeforeWrite { get; set; }

    public bool SupportsTransaction => inner.SupportsTransaction;

    public Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken) => inner.ReadAsync(key, cancellationToken);

    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken)
        => TryWriteAsync(key, value, expectedETag, null, cancellationToken);

    public async Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, TimeSpan? timeToLive, CancellationToken cancellationToken)
    {
        if (BeforeWrite is not null) await BeforeWrite(key, value, cancellationToken);
        bool accepted = false;
        try
        {
            accepted = await inner.TryWriteAsync(key, value, expectedETag, timeToLive, cancellationToken);
            return accepted;
        }
        finally
        {
            _writes.Add((key, expectedETag, accepted));
        }
    }

    public async Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken)
    {
        bool accepted = false;
        try
        {
            accepted = await inner.TryDeleteAsync(key, expectedETag, cancellationToken);
            return accepted;
        }
        finally
        {
            _deletes.Add((key, accepted));
        }
    }

    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken)
        => inner.ExecuteTransactionAsync(operations, cancellationToken);
}
