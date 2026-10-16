using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

namespace FoldedSnapshotCompatibility;

/// <summary>Snapshot manager compiled against the published package with only the original writer method.</summary>
public sealed class LegacySnapshotManager : ISnapshotManager
{
    /// <summary>Gets the number of original writer calls.</summary>
    public static int Calls { get; private set; }

    /// <summary>Gets the exact identity received by the original writer.</summary>
    public static AggregateIdentity? LastIdentity { get; private set; }

    /// <summary>Gets the covered sequence received by the original writer.</summary>
    public static long LastSequenceNumber { get; private set; }

    /// <summary>Gets the exact folded state received by the original writer.</summary>
    public static object? LastState { get; private set; }

    /// <summary>Gets the exact actor manager received by the original writer.</summary>
    public static IActorStateManager? LastStateManager { get; private set; }

    /// <summary>Gets the correlation received by the original writer.</summary>
    public static string? LastCorrelationId { get; private set; }

    /// <summary>Gets the originating cancellation token received by the original writer.</summary>
    public static CancellationToken LastCancellationToken { get; private set; }

    /// <summary>Gets the failure posture received by the original writer.</summary>
    public static bool LastThrowOnFailure { get; private set; }

    /// <summary>Gets or sets a provider failure that the original writer handles according to its failure posture.</summary>
    public static Exception? ProviderFailure { get; set; }

    /// <inheritdoc/>
    public Task CreateSnapshotAsync(AggregateIdentity identity, long sequenceNumber, object state,
        IActorStateManager stateManager, string? correlationId = null,
        CancellationToken cancellationToken = default, bool throwOnFailure = false)
    {
        Calls++;
        LastIdentity = identity;
        LastSequenceNumber = sequenceNumber;
        LastState = state;
        LastStateManager = stateManager;
        LastCorrelationId = correlationId;
        LastCancellationToken = cancellationToken;
        LastThrowOnFailure = throwOnFailure;
        return throwOnFailure && ProviderFailure is not null
            ? Task.FromException(ProviderFailure) : Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ShouldCreateSnapshotAsync(string tenantId, string domain, string aggregateType,
        long currentSequence, long lastSnapshotSequence, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> ShouldCreateSnapshotAsync(string tenantId, string domain,
        long currentSequence, long lastSnapshotSequence)
        => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<SnapshotRecord?> LoadSnapshotAsync(AggregateIdentity identity, IActorStateManager stateManager,
        string? correlationId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<SnapshotRecord?>(null);

    /// <inheritdoc/>
    public Task<SnapshotLoadResult> InspectSnapshotForManualOverwriteAsync(AggregateIdentity identity,
        IActorStateManager stateManager, string? correlationId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(SnapshotLoadResult.Absent());
}
