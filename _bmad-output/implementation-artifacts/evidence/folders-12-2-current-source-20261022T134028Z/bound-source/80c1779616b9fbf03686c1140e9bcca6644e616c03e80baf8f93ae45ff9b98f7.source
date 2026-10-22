using Hexalith.EventStore.Client.Projections;
namespace Hexalith.EventStore.Client.Tests.Projections;
internal sealed class RejectingBatchStateAccessor : IReadModelBatchStateAccessor
{
    public int Accesses { get; private set; }
    public bool SupportsTransaction => Reject<bool>();
    public Task<RawStateEntry> ReadAsync(string key, CancellationToken cancellationToken) => Reject<Task<RawStateEntry>>();
    public Task<bool> TryWriteAsync(string key, ReadOnlyMemory<byte> value, string expectedETag, CancellationToken cancellationToken) => Reject<Task<bool>>();
    public Task<bool> TryDeleteAsync(string key, string expectedETag, CancellationToken cancellationToken) => Reject<Task<bool>>();
    public Task ExecuteTransactionAsync(IReadOnlyList<RawTransactionOperation> operations, CancellationToken cancellationToken) => Reject<Task>();
    private T Reject<T>() { Accesses++; throw new InvalidOperationException("No state access is permitted before validating recovery bounds."); }
}
