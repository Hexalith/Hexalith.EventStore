namespace Hexalith.EventStore.Testing.Fakes;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

/// <summary>
/// In-memory implementation of <see cref="ISnapshotManager"/> for unit testing.
/// Records all snapshot operations for test assertions.
/// </summary>
public sealed class FakeSnapshotManager : ISnapshotManager
{
    private readonly Dictionary<string, SnapshotRecord> _snapshots = [];
    private readonly List<(string Domain, long CurrentSequence, long LastSnapshotSequence)> _shouldCreateCalls = [];
    private readonly List<(AggregateIdentity Identity, long SequenceNumber, object State)> _createCalls = [];
    private readonly List<AggregateIdentity> _loadCalls = [];

    /// <summary>Gets the stored snapshots keyed by snapshot key.</summary>
    public IReadOnlyDictionary<string, SnapshotRecord> Snapshots => _snapshots;

    /// <summary>Gets the recorded ShouldCreateSnapshotAsync calls.</summary>
    public IReadOnlyList<(string Domain, long CurrentSequence, long LastSnapshotSequence)> ShouldCreateCalls => _shouldCreateCalls;

    /// <summary>Gets the recorded CreateSnapshotAsync calls.</summary>
    public IReadOnlyList<(AggregateIdentity Identity, long SequenceNumber, object State)> CreateCalls => _createCalls;

    /// <summary>Gets the recorded LoadSnapshotAsync calls.</summary>
    public IReadOnlyList<AggregateIdentity> LoadCalls => _loadCalls;

    /// <summary>
    /// Gets or sets the default interval used by <see cref="ShouldCreateSnapshotAsync"/>.
    /// Default: 100.
    /// </summary>
    public int DefaultInterval { get; set; } = 100;

    /// <summary>
    /// Gets or sets per-domain interval overrides for <see cref="ShouldCreateSnapshotAsync"/>.
    /// </summary>
    public Dictionary<string, int> DomainIntervals { get; set; } = [];

    /// <inheritdoc/>
    public Task<bool> ShouldCreateSnapshotAsync(string domain, long currentSequence, long lastSnapshotSequence)
    {
        _shouldCreateCalls.Add((domain, currentSequence, lastSnapshotSequence));

        int interval = DomainIntervals.TryGetValue(domain, out int domainInterval)
            ? domainInterval
            : DefaultInterval;

        bool shouldCreate = (currentSequence - lastSnapshotSequence) >= interval;
        return Task.FromResult(shouldCreate);
    }

    /// <inheritdoc/>
    public Task CreateSnapshotAsync(AggregateIdentity identity, long sequenceNumber, object state, IActorStateManager stateManager, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stateManager);

        _createCalls.Add((identity, sequenceNumber, state));

        var snapshot = new SnapshotRecord(
            SequenceNumber: sequenceNumber,
            State: state,
            CreatedAt: DateTimeOffset.UtcNow,
            Domain: identity.Domain,
            AggregateId: identity.AggregateId,
            TenantId: identity.TenantId);

        _snapshots[identity.SnapshotKey] = snapshot;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SnapshotRecord?> LoadSnapshotAsync(AggregateIdentity identity, IActorStateManager stateManager, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(stateManager);

        _loadCalls.Add(identity);

        SnapshotRecord? result = _snapshots.TryGetValue(identity.SnapshotKey, out SnapshotRecord? snapshot)
            ? snapshot
            : null;

        return Task.FromResult(result);
    }
}
