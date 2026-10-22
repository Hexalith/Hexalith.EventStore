using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>A side-effect-free named projection candidate for one coordinated rebuild promotion.</summary>
public sealed class DomainProjectionRebuildPlan {
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

    /// <summary>Gets the state-store component that owns the candidate operations.</summary>
    public string StoreName { get; }

    /// <summary>Gets the immutable candidate operation snapshot.</summary>
    public IReadOnlyList<ReadModelBatchOperation> Operations { get; }

    /// <summary>Gets a defensive copy of the opaque post-commit completion state.</summary>
    public ReadOnlyMemory<byte> CompletionState { get; }
}
