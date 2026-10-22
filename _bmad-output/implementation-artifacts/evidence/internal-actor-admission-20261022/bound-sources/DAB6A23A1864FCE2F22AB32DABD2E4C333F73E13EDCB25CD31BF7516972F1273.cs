namespace Hexalith.EventStore.Client.Projections;

/// <summary>Optional marker-gated staging companion for rebuild promotion.</summary>
public interface IReadModelBatchStagingStore {
    /// <summary>Installs operation envelopes while the previous complete view remains visible.</summary>
    Task<ReadModelBatchStagingResult> StageAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default);

    /// <summary>Commits a previously staged manifest and verifies its durable visible result.</summary>
    Task<ReadModelBatchStagingResult> CommitAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default);

    /// <summary>Restores the previous view for a previously staged, uncommitted manifest.</summary>
    Task<ReadModelBatchStagingResult> AbortAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default);

    /// <summary>Reads back marker and operation evidence for a staged or committed manifest.</summary>
    Task<ReadModelBatchStagingResult> VerifyAsync(
        ReadModelBatch batch,
        CancellationToken cancellationToken = default);
}
