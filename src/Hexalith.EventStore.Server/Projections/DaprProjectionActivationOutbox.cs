using Hexalith.EventStore.ServiceDefaults.Routing;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR state-store implementation of the projection activation outbox.</summary>
public sealed partial class DaprProjectionActivationOutbox(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions,
    TimeProvider timeProvider) : IProjectionActivationOutbox, IProjectionLegacyDeliveryOutbox {
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
        (ProjectionActivationLedger? ledger, _) = await ReadLedgerAsync(GetKey(workItem.ActivationId), cancellationToken).ConfigureAwait(false);
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
                && item.Revision == workItem.Revision
                && item.LegacyDelivery is null) > 0,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionActivationWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationScans);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationShardReads, Keys.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads, Keys.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        IReadOnlyList<BulkStateItem> shards = await RouteCatalogRemoteCall.InvokeAsync(() => daprClient
            .GetBulkStateAsync(
                projectionOptions.Value.CheckpointStateStoreName,
                Keys,
                BulkReadParallelism,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        var items = new List<ProjectionActivationWorkItem>();
        foreach (BulkStateItem shard in shards)
        {
            long bytes = shard.Value is null ? 0 : Encoding.UTF8.GetByteCount(shard.Value);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, bytes);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationReadBytes, bytes);
            ProjectionActivationLedger? ledger = string.IsNullOrEmpty(shard.Value) ? null
                : JsonSerializer.Deserialize<ProjectionActivationLedger>(shard.Value, daprClient.JsonSerializerOptions);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationEntries, ledger?.Items.Count ?? 0);
            items.AddRange(ledger?.Items ?? []);
        }
        return [.. items
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
        CancellationToken cancellationToken, int? maxLedgerBytes = null) {
        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        string key = GetKey(activationId);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            (ProjectionActivationLedger? ledger, string etag) = await ReadLedgerAsync(key, cancellationToken).ConfigureAwait(false);
            List<ProjectionActivationWorkItem> items = [.. ledger?.Items ?? []];
            if (!mutation(items)) {
                return;
            }

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new ProjectionActivationLedger(items), daprClient.JsonSerializerOptions);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSerializationBytes, bytes.Length);
            if (maxLedgerBytes is { } limit && bytes.Length > limit)
                throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationWriteAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationWriteBytes, bytes.Length);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, bytes.Length);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            if (await RouteCatalogRemoteCall.InvokeAsync(() => daprClient.TrySaveByteStateAsync(
                    storeName,
                    key,
                    bytes,
                    etag,
                    stateOptions: new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false)) {
                return;
            }
        }

        throw new InvalidOperationException("Projection activation outbox update exhausted optimistic-concurrency attempts.");
    }

    private async Task<(ProjectionActivationLedger? Ledger, string Etag)> ReadLedgerAsync(string key, CancellationToken cancellationToken)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationShardReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> body, string etag) = await RouteCatalogRemoteCall.InvokeAsync(() => daprClient.GetByteStateAndETagAsync(
            projectionOptions.Value.CheckpointStateStoreName, key, cancellationToken: cancellationToken)).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, body.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationReadBytes, body.Length);
        ProjectionActivationLedger? ledger = body.IsEmpty ? null : JsonSerializer.Deserialize<ProjectionActivationLedger>(body.Span, daprClient.JsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ActivationEntries, ledger?.Items.Count ?? 0);
        return (ledger, etag);
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
