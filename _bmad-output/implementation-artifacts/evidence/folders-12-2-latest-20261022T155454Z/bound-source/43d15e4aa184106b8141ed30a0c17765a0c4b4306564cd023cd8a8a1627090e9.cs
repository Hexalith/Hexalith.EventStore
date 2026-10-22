using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Testing.Integration.Benchmarking;

namespace Hexalith.EventStore.Testing.Integration.Tests.Benchmarking;

internal sealed class RecordingBenchmarkActorLifecycle : IBenchmarkActorLifecycle {
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AggregateStreamMetadata> _metadata = new(StringComparer.Ordinal);
    private int _activationCount;
    private int _deactivationCount;

    internal int ActivationCount => Volatile.Read(ref _activationCount);

    internal Exception? DeactivationException { get; set; }

    internal int DeactivationFailureAtCount { get; set; }

    internal int DeactivationCount => Volatile.Read(ref _deactivationCount);

    public Task<AggregateStreamMetadata> ActivateAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _activationCount);
        lock (_lock) {
            return Task.FromResult(
                _metadata.TryGetValue(identity.ActorId, out AggregateStreamMetadata metadata)
                    ? metadata
                    : new AggregateStreamMetadata(Exists: false, CurrentSequence: 0));
        }
    }

    public Task DeactivateAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        int deactivationCount = Interlocked.Increment(ref _deactivationCount);
        if (DeactivationException is not null
            && (DeactivationFailureAtCount == 0 || DeactivationFailureAtCount == deactivationCount)) {
            return Task.FromException(DeactivationException);
        }

        return Task.CompletedTask;
    }

    internal void SetMetadata(AggregateIdentity identity, AggregateStreamMetadata metadata) {
        lock (_lock) {
            _metadata[identity.ActorId] = metadata;
        }
    }
}
