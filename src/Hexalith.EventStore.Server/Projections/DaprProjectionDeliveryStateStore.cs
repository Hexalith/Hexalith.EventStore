using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR implementation of the projection delivery state and writer protocol store.</summary>
internal sealed class DaprProjectionDeliveryStateStore(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions) : IProjectionDeliveryStateStore {
    /// <inheritdoc/>
    public async Task<ProjectionDeliveryStateReadResult> ReadAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        (ProjectionDeliveryState? state, string etag) = await daprClient
            .GetStateAndETagAsync<ProjectionDeliveryState>(
                storeName,
                ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ProjectionDeliveryWriterProtocol? marker = await ReadWriterProtocolAsync(cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryStateClassification classification = ProjectionDeliveryStateClassifier.Classify(
            state,
            marker?.IsCurrent == true);
        return new ProjectionDeliveryStateReadResult(state, etag, classification, marker?.IsCurrent == true);
    }

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        string etag,
        CancellationToken cancellationToken = default) {
        ValidateState(identity, projectionName, state);
        ArgumentNullException.ThrowIfNull(etag);

        return await daprClient.TrySaveStateAsync(
            projectionOptions.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName),
            state,
            etag,
            new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TrySaveWithReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state,
        ProjectionDeliveryReconciliationWork work,
        string etag,
        CancellationToken cancellationToken = default) {
        ValidateState(identity, projectionName, state);
        ValidateReconciliationWork(identity, projectionName, work);
        ArgumentNullException.ThrowIfNull(etag);

        var firstWrite = new StateOptions { Concurrency = ConcurrencyMode.FirstWrite };
        await daprClient.ExecuteStateTransactionAsync(
            projectionOptions.Value.CheckpointStateStoreName,
            [
                new StateTransactionRequest(
                    ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName),
                    JsonSerializer.SerializeToUtf8Bytes(state, daprClient.JsonSerializerOptions),
                    StateOperationType.Upsert,
                    etag,
                    options: firstWrite),
                new StateTransactionRequest(
                    ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName),
                    JsonSerializer.SerializeToUtf8Bytes(work, daprClient.JsonSerializerOptions),
                    StateOperationType.Upsert),
            ],
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryWriterProtocol?> ReadWriterProtocolAsync(
        CancellationToken cancellationToken = default) =>
        await daprClient.GetStateAsync<ProjectionDeliveryWriterProtocol>(
            projectionOptions.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.WriterProtocol,
            cancellationToken: cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> TryActivateWriterProtocolAsync(
        ProjectionDeliveryWriterProtocol marker,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(marker);
        if (!marker.IsCurrent) {
            throw new ArgumentException("Projection delivery writer protocol marker is not current.", nameof(marker));
        }

        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        (ProjectionDeliveryWriterProtocol? existing, string etag) = await daprClient
            .GetStateAndETagAsync<ProjectionDeliveryWriterProtocol>(
                storeName,
                ProjectionDeliveryStateKeys.WriterProtocol,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null) {
            return existing.IsCurrent
                && string.Equals(existing.CutoverCommit, marker.CutoverCommit, StringComparison.Ordinal);
        }

        return await daprClient.TrySaveStateAsync(
            storeName,
            ProjectionDeliveryStateKeys.WriterProtocol,
            marker,
            etag,
            new StateOptions { Concurrency = ConcurrencyMode.FirstWrite },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RecordReconciliationAsync(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReconciliationWork work,
        CancellationToken cancellationToken = default) {
        ValidateReconciliationWork(identity, projectionName, work);

        await daprClient.SaveStateAsync(
            projectionOptions.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName),
            work,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateReconciliationWork(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryReconciliationWork work) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(work);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        if (!string.Equals(work.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(work.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(work.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            || !string.Equals(work.ProjectionName, projectionName, StringComparison.Ordinal)) {
            throw new ArgumentException("Projection delivery reconciliation work does not match the requested scope.", nameof(work));
        }
    }

    private static void ValidateState(
        AggregateIdentity identity,
        string projectionName,
        ProjectionDeliveryState state) {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        if (state.SchemaVersion != ProjectionDeliveryState.CurrentSchemaVersion
            || state.WriterProtocolVersion != ProjectionDeliveryState.CurrentWriterProtocolVersion
            || !string.Equals(state.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(state.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(state.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            || !string.Equals(state.ProjectionName, projectionName, StringComparison.Ordinal)) {
            throw new ArgumentException("Projection delivery state does not match the current writer and requested scope.", nameof(state));
        }
    }
}
