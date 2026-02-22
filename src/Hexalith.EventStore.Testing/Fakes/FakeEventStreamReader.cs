
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Testing.Fakes;
/// <summary>
/// In-memory implementation of <see cref="IEventStreamReader"/> for unit testing.
/// Records all rehydration calls for test assertions and returns configurable results.
/// </summary>
public sealed class FakeEventStreamReader : IEventStreamReader {
    private readonly List<(AggregateIdentity Identity, SnapshotRecord? Snapshot)> _rehydrateCalls = [];

    /// <summary>Gets the recorded RehydrateAsync calls.</summary>
    public IReadOnlyList<(AggregateIdentity Identity, SnapshotRecord? Snapshot)> RehydrateCalls => _rehydrateCalls;

    /// <summary>
    /// Gets or sets the result to return from <see cref="RehydrateAsync"/>.
    /// Set to null to simulate a new aggregate.
    /// </summary>
    public RehydrationResult? ResultToReturn { get; set; }

    /// <inheritdoc/>
    public Task<RehydrationResult?> RehydrateAsync(AggregateIdentity identity, SnapshotRecord? snapshot = null) {
        ArgumentNullException.ThrowIfNull(identity);
        _rehydrateCalls.Add((identity, snapshot));
        return Task.FromResult(ResultToReturn);
    }
}
