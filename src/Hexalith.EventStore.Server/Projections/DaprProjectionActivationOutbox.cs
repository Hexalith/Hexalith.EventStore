using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR state-store implementation of the projection activation outbox.</summary>
public sealed class DaprProjectionActivationOutbox(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions,
    TimeProvider timeProvider) : IProjectionActivationOutbox {
    private const int ShardCount = 64;
    private const int MaxEtagRetries = 5;
    private const int BulkReadParallelism = 8;
    private const string KeyPrefix = "projection-activation:outbox:v1:";
    private static readonly string[] Keys = [.. Enumerable.Range(0, ShardCount).Select(static value => $"{KeyPrefix}{value:x2}")];

    /// <inheritdoc/>
    public async Task EnsureAsync(AggregateIdentity identity, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionActivationWorkItem workItem = ProjectionActivationWorkItem.Create(identity, timeProvider.GetUtcNow());
        await MutateAsync(
            workItem.ActivationId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.ActivationId, workItem.ActivationId, StringComparison.Ordinal));
                if (index >= 0) {
                    ProjectionActivationWorkItem current = items[index];
                    items[index] = current with {
                        Revision = current.Revision + 1,
                        Attempt = 0,
                        NextDueUtc = workItem.NextDueUtc,
                    };
                    return true;
                }

                items.Add(workItem);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ProjectionActivationWorkItem?> GetAsync(
        AggregateIdentity identity,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionActivationWorkItem workItem = ProjectionActivationWorkItem.Create(identity, timeProvider.GetUtcNow());
        (ProjectionActivationLedger? ledger, _) = await daprClient
            .GetStateAndETagAsync<ProjectionActivationLedger>(
                projectionOptions.Value.CheckpointStateStoreName,
                GetKey(workItem.ActivationId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ledger?.Items.FirstOrDefault(item =>
            string.Equals(item.ActivationId, workItem.ActivationId, StringComparison.Ordinal)
            && string.Equals(item.TenantId, identity.TenantId, StringComparison.Ordinal)
            && string.Equals(item.Domain, identity.Domain, StringComparison.Ordinal)
            && string.Equals(item.AggregateId, identity.AggregateId, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public Task CompleteAsync(
        ProjectionActivationWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        return MutateAsync(
            workItem.ActivationId,
            items => items.RemoveAll(item =>
                string.Equals(item.ActivationId, workItem.ActivationId, StringComparison.Ordinal)
                && item.Revision == workItem.Revision) > 0,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        IReadOnlyList<BulkStateItem<ProjectionActivationLedger>> shards = await daprClient
            .GetBulkStateAsync<ProjectionActivationLedger>(
                projectionOptions.Value.CheckpointStateStoreName,
                Keys,
                BulkReadParallelism,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return [.. shards
            .SelectMany(static shard => shard.Value?.Items ?? [])
            .Where(static item => IsValid(item))
            .Where(item => item.NextDueUtc <= dueUtc)
            .OrderBy(static item => item.NextDueUtc)
            .ThenBy(static item => item.ActivationId, StringComparer.Ordinal)
            .Take(maximumCount)];
    }

    /// <inheritdoc/>
    public Task DeferAsync(
        ProjectionActivationWorkItem workItem,
        DateTimeOffset nextDueUtc,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        return MutateAsync(
            workItem.ActivationId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.ActivationId, workItem.ActivationId, StringComparison.Ordinal));
                if (index < 0) {
                    return false;
                }

                ProjectionActivationWorkItem current = items[index];
                if (current.Revision != workItem.Revision) {
                    return false;
                }

                items[index] = current with {
                    Attempt = current.Attempt + 1,
                    NextDueUtc = nextDueUtc,
                };
                return true;
            },
            cancellationToken);
    }

    private async Task MutateAsync(
        string activationId,
        Func<List<ProjectionActivationWorkItem>, bool> mutation,
        CancellationToken cancellationToken) {
        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        string key = GetKey(activationId);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            (ProjectionActivationLedger? ledger, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionActivationLedger>(storeName, key, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            List<ProjectionActivationWorkItem> items = [.. ledger?.Items ?? []];
            if (!mutation(items)) {
                return;
            }

            if (await daprClient.TrySaveStateAsync(
                    storeName,
                    key,
                    new ProjectionActivationLedger(items),
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false)) {
                return;
            }
        }

        throw new InvalidOperationException("Projection activation outbox update exhausted optimistic-concurrency attempts.");
    }

    private static string GetKey(string activationId) {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(activationId));
        return Keys[hash[0] % ShardCount];
    }

    private static bool IsValid(ProjectionActivationWorkItem? item)
        => item is not null
            && !string.IsNullOrWhiteSpace(item.ActivationId)
            && !string.IsNullOrWhiteSpace(item.TenantId)
            && !string.IsNullOrWhiteSpace(item.Domain)
            && !string.IsNullOrWhiteSpace(item.AggregateId)
            && item.Revision > 0
            && item.Attempt >= 0;
}
