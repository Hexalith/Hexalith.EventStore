using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Reads and compare-and-swaps checkpointed projection records through <see cref="IReadModelStore"/>,
/// classifying provider failures and malformed records as retryable unavailability.
/// </summary>
internal static class CheckpointedProjectionStorage {
    internal static async Task<(CheckpointedProjectionRecord<TState>? Record, string ETag)> LoadAsync<TState>(
        IReadModelStore store,
        string storeName,
        string key,
        CancellationToken cancellationToken)
        where TState : class {
        ReadModelEntry<CheckpointedProjectionRecord<TState>>? entry;
        try {
            entry = await store
                .GetAsync<CheckpointedProjectionRecord<TState>>(storeName, key, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (JsonException exception) {
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.MalformedRecord, exception);
        }
        catch (Exception exception) {
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.ProviderUnavailable, exception);
        }

        if (entry?.Value is not { } record) {
            return (null, string.Empty);
        }

        if (!record.IsWellFormed() || string.IsNullOrEmpty(entry.ETag)) {
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.MalformedRecord);
        }

        return (record, entry.ETag);
    }

    internal static async Task<bool> TrySaveAsync<TState>(
        IReadModelStore store,
        string storeName,
        string key,
        CheckpointedProjectionRecord<TState> record,
        string etag,
        CancellationToken cancellationToken)
        where TState : class {
        try {
            return await store.TrySaveAsync(storeName, key, record, etag, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) {
            throw new CheckpointedProjectionUnavailableException(CheckpointedProjectionUnavailableReason.ProviderUnavailable, exception);
        }
    }

    internal static CheckpointedProjectionRecord<TState> MarkCorrupt<TState>(
        CheckpointedProjectionRecord<TState> current,
        CheckpointedProjectionCorruptionReason reason,
        long sequence)
        where TState : class
        => current with {
            Health = CheckpointedProjectionHealth.Corrupt,
            CorruptionReason = reason,
            CorruptSequence = sequence,
            Lease = null,
            Version = current.Version + 1,
        };
}
