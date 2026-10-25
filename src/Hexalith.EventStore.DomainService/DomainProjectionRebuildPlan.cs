using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>A side-effect-free named projection candidate for one coordinated rebuild promotion.</summary>
public sealed class DomainProjectionRebuildPlan {
    private IReadOnlyList<ReadModelBatchOperation> _stagedOperations = [];
    /// <summary>Initializes a rebuild plan with an immutable operation snapshot.</summary>
    /// <param name="storeName">The single state-store component that owns every operation.</param>
    /// <param name="operations">The candidate read-model writes or deletes.</param>
    /// <param name="completionState">
    /// Optional opaque handler-owned state captured before commit and retained for idempotent
    /// <see cref="IAsyncDomainSharedProjectionRebuildCompletionHandler"/> processing.
    /// </param>
    public DomainProjectionRebuildPlan(
        string storeName,
        IEnumerable<ReadModelBatchOperation> operations,
        ReadOnlyMemory<byte> completionState = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeName);
        ArgumentNullException.ThrowIfNull(operations);
        StoreName = storeName;
        Operations = [.. operations];
        if (Operations.Count == 0) {
            throw new ArgumentException("A projection rebuild plan must contain at least one operation.", nameof(operations));
        }

        CompletionState = completionState.ToArray();
    }

    /// <summary>Opts this plan into aggregate-discoverable private preparation and erasure fences.
    /// Other owners retain their existing private recovery policy by default.</summary>
    public bool DiscoverableRecovery { get; init; }

    /// <summary>Gets the state-store component that owns the candidate operations.</summary>
    public string StoreName { get; }

    /// <summary>Gets the immutable candidate operation snapshot.</summary>
    public IReadOnlyList<ReadModelBatchOperation> Operations { get; }

    /// <summary>Gets disjoint immutable generation writes to stage before the coordinated canonical promotion.</summary>
    public IReadOnlyList<ReadModelBatchOperation> StagedOperations {
        get => _stagedOperations;
        init {
            ArgumentNullException.ThrowIfNull(value);
            _stagedOperations = [.. value];
        }
    }

    /// <summary>Gets a defensive copy of the opaque post-commit completion state.</summary>
    public ReadOnlyMemory<byte> CompletionState { get; }
}
