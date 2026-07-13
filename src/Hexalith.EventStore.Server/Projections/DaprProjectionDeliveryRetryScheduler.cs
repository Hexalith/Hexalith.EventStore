using Dapr.Client;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR state-store backed immediate-mode retry scheduler.</summary>
public sealed class DaprProjectionDeliveryRetryScheduler(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions) : IProjectionDeliveryRetryScheduler {
    private const int MaxEtagRetries = 5;
    private const string LedgerKey = "projection-delivery-retry:ledger:v1";

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryRetryWorkItem> ScheduleAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ValidateWorkItem(workItem);
        ProjectionDeliveryRetryWorkItem? persisted = null;
        await MutateAsync(
            items => {
                ProjectionDeliveryRetryWorkItem? existing = items.FirstOrDefault(
                    item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                persisted = existing ?? workItem;
                if (existing is null) {
                    items.Add(workItem);
                }

                return existing is null;
            },
            cancellationToken).ConfigureAwait(false);
        return persisted ?? workItem;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ValidateWorkItem(workItem);
        return MutateAsync(
            items => {
                int index = items.FindIndex(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                if (index >= 0) {
                    items[index] = workItem;
                }
                else {
                    items.Add(workItem);
                }

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string workId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(workId);
        return MutateAsync(
            items => items.RemoveAll(item => string.Equals(item.WorkId, workId, StringComparison.Ordinal)) > 0,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionDeliveryRetryWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        ProjectionDeliveryRetryLedger? ledger = await daprClient
            .GetStateAsync<ProjectionDeliveryRetryLedger>(
                projectionOptions.Value.CheckpointStateStoreName,
                LedgerKey,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return [.. (ledger?.Items ?? [])
            .Where(item => item.NextDueUtc <= dueUtc && item.PendingRoutes.Count > 0)
            .OrderBy(static item => item.NextDueUtc)
            .ThenBy(static item => item.WorkId, StringComparer.Ordinal)
            .Take(maximumCount)];
    }

    private async Task MutateAsync(
        Func<List<ProjectionDeliveryRetryWorkItem>, bool> mutation,
        CancellationToken cancellationToken) {
        string stateStoreName = projectionOptions.Value.CheckpointStateStoreName;
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            (ProjectionDeliveryRetryLedger? ledger, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                    stateStoreName,
                    LedgerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            List<ProjectionDeliveryRetryWorkItem> items = [.. ledger?.Items ?? []];
            if (!mutation(items)) {
                return;
            }

            bool saved = await daprClient
                .TrySaveStateAsync(
                    stateStoreName,
                    LedgerKey,
                    new ProjectionDeliveryRetryLedger(items),
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (saved) {
                return;
            }
        }

        throw new InvalidOperationException("Projection delivery retry ledger update exhausted optimistic-concurrency attempts.");
    }

    private static void ValidateWorkItem(ProjectionDeliveryRetryWorkItem workItem) {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.WorkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.AggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.AppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.ServiceVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workItem.HeadSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.HeadMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.DispatchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.CatalogFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegative(workItem.Attempt);
        if (workItem.PendingRoutes.Count == 0 && workItem.TerminalRoutes.Count == 0) {
            throw new ArgumentException("Retry work must retain at least one pending or terminal route.", nameof(workItem));
        }
    }
}
