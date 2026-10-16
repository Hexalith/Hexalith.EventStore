using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR implementation of the projection delivery state and writer protocol store.</summary>
internal sealed class DaprProjectionDeliveryStateStore(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions,
    IOptions<ProjectionDeliveryIdempotencyOptions>? idempotencyOptions = null) : IProjectionDeliveryStateStore {
    /// <inheritdoc/>
    public async Task<ProjectionDeliveryStateReadResult> ReadAsync(
        AggregateIdentity identity,
        string projectionName,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> stateBody, string etag) = await daprClient
            .GetByteStateAndETagAsync(
                storeName,
                ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofReadBytes, stateBody.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofParsingBytes, stateBody.Length);
        ProjectionDeliveryState? state = stateBody.IsEmpty ? null
            : JsonSerializer.Deserialize<ProjectionDeliveryState>(stateBody.Span, daprClient.JsonSerializerOptions);
        if (state is not null) ValidateSize(state, stateBody.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MarkerReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> writerBody, _) = await daprClient.GetByteStateAndETagAsync(storeName,
            ProjectionDeliveryStateKeys.WriterProtocol, cancellationToken: cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MarkerReadBytes, writerBody.Length);
        if (writerBody.Length > 4096) throw new JsonException("Invalid writer protocol record size.");
        ProjectionDeliveryWriterProtocol? marker = writerBody.IsEmpty ? null
            : JsonSerializer.Deserialize<ProjectionDeliveryWriterProtocol>(writerBody.Span, daprClient.JsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MarkerReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> guardBody, string guardEtag) = await daprClient.GetByteStateAndETagAsync(storeName,
            ProjectionDeliveryStateKeys.GetSourceGuardKey(identity, projectionName),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MarkerReadBytes, guardBody.Length);
        if (guardBody.Length > 4096) throw new JsonException("Invalid source guard record size.");
        using JsonDocument? guardJson = guardBody.IsEmpty ? null : JsonDocument.Parse(guardBody);
        ProjectionSourceGuard? guard = guardJson is null ? null : ProjectionSourceGuard.Parse(guardJson.RootElement, daprClient.JsonSerializerOptions);
        if (guard is not null && !guard.IsValid(identity, projectionName))
        {
            throw new JsonException("Source guard scope or version is invalid.");
        }
        ProjectionDeliveryStateClassification classification = ProjectionDeliveryStateClassifier.Classify(
            state,
            marker?.IsCurrent == true);
        return new ProjectionDeliveryStateReadResult(state, etag, classification, marker?.IsCurrent == true)
        {
            SourceGuard = guard,
            SourceGuardEtag = guardEtag,
            StateBytes = stateBody.Length,
            MarkerBytes = writerBody.Length + guardBody.Length,
        };
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
        byte[] bytes = ProjectionDeliveryStateSerialization.Serialize(state, daprClient.JsonSerializerOptions, idempotencyOptions?.Value.CompletedReceiptLimit ?? 256);
        ValidateSize(state, bytes.Length);

        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofWriteAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofWriteBytes, bytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        return await daprClient.TrySaveByteStateAsync(
            projectionOptions.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName),
            bytes,
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
        byte[] stateBytes = ProjectionDeliveryStateSerialization.Serialize(state, daprClient.JsonSerializerOptions, idempotencyOptions?.Value.CompletedReceiptLimit ?? 256);
        byte[] workBytes = SerializeWork(work);
        ValidateSize(state, stateBytes.Length);

        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofWriteAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofWriteBytes, stateBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReconciliationWrites);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, workBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.TransactionAttempts);
        var firstWrite = new StateOptions { Concurrency = ConcurrencyMode.FirstWrite };
        await daprClient.ExecuteStateTransactionAsync(
            projectionOptions.Value.CheckpointStateStoreName,
            [
                new StateTransactionRequest(
                    ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName),
                    stateBytes,
                    StateOperationType.Upsert,
                    etag,
                    options: firstWrite),
                new StateTransactionRequest(
                    ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName),
                    workBytes,
                    StateOperationType.Upsert),
            ],
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryWriterProtocol?> ReadWriterProtocolAsync(
        CancellationToken cancellationToken = default)
    {
        (ReadOnlyMemory<byte> body, _) = await ReadWriterBodyAsync(cancellationToken).ConfigureAwait(false);
        return body.IsEmpty ? null : JsonSerializer.Deserialize<ProjectionDeliveryWriterProtocol>(body.Span, daprClient.JsonSerializerOptions);
    }

    /// <inheritdoc/>
    public async Task<bool> TryActivateWriterProtocolAsync(
        ProjectionDeliveryWriterProtocol marker,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(marker);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.WriterActivationAttempts);
        if (!marker.IsCurrent) {
            throw new ArgumentException("Projection delivery writer protocol marker is not current.", nameof(marker));
        }

        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        (ReadOnlyMemory<byte> body, string etag) = await ReadWriterBodyAsync(cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryWriterProtocol? existing = body.IsEmpty ? null
            : JsonSerializer.Deserialize<ProjectionDeliveryWriterProtocol>(body.Span, daprClient.JsonSerializerOptions);
        if (existing is not null) {
            return existing.IsCurrent
                && string.Equals(existing.CutoverCommit, marker.CutoverCommit, StringComparison.Ordinal);
        }

        byte[] markerBytes = JsonSerializer.SerializeToUtf8Bytes(marker, daprClient.JsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSerializationBytes, markerBytes.Length);
        if (markerBytes.Length > 4096) throw new JsonException("Invalid writer protocol record size.");
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, markerBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        return await daprClient.TrySaveByteStateAsync(
            storeName,
            ProjectionDeliveryStateKeys.WriterProtocol,
            markerBytes,
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
        byte[] bytes = SerializeWork(work);

        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReconciliationWrites);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, bytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        await daprClient.SaveByteStateAsync(
            projectionOptions.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName),
            bytes,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> TryActivateSourceGuardAsync(AggregateIdentity identity, string projectionName,
        ProjectionDeliveryState state, ProjectionSourceGuard marker, ProjectionDeliveryReconciliationWork work,
        string stateEtag, string markerEtag, CancellationToken cancellationToken = default)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.GuardActivationAttempts);
        ValidateState(identity, projectionName, state);
        ValidateReconciliationWork(identity, projectionName, work);
        ArgumentNullException.ThrowIfNull(stateEtag);
        ArgumentNullException.ThrowIfNull(markerEtag);
        if (!marker.IsValid(identity, projectionName) || work.SourceGuardMaintenance?.IsValid != true
            || work.QualifiedSourceGuard != marker || work.OperatorId != work.SourceGuardMaintenance.OperatorId
            || marker.CutoverCommit != work.SourceGuardMaintenance.CutoverCommit
            || state.SchemaVersion != 2 || state.GuardEvidenceVersion != 1 || state.ActiveReservation is not null
            || state.IdleBindingPending is not null || !ValidActivationEvidence(state, marker, identity, projectionName)
            || !ProjectionDeliveryIdempotencyCoordinator.ValidCompleteIdentityDigests(state))
            return false;
        byte[] stateBytes = ProjectionDeliveryStateSerialization.Serialize(state, daprClient.JsonSerializerOptions, idempotencyOptions?.Value.CompletedReceiptLimit ?? 256);
        ValidateSize(state, stateBytes.Length);
        byte[] markerBytes = JsonSerializer.SerializeToUtf8Bytes(marker, daprClient.JsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSerializationBytes, markerBytes.Length);
        if (markerBytes.Length > 4096) throw new JsonException("Invalid source guard record size.");
        byte[] workBytes = SerializeWork(work);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofWriteAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofWriteBytes, stateBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReconciliationWrites);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts, 2);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, markerBytes.Length + workBytes.Length);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.TransactionAttempts);
        var firstWrite = new StateOptions { Concurrency = ConcurrencyMode.FirstWrite };
        await daprClient.ExecuteStateTransactionAsync(projectionOptions.Value.CheckpointStateStoreName,
        [
            new(ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName), stateBytes,
                StateOperationType.Upsert, stateEtag, options: firstWrite),
            new(ProjectionDeliveryStateKeys.GetSourceGuardKey(identity, projectionName), markerBytes,
                StateOperationType.Upsert, markerEtag, options: firstWrite),
            new(ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName), workBytes, StateOperationType.Upsert),
        ], cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool ValidActivationEvidence(ProjectionDeliveryState state, ProjectionSourceGuard marker,
        AggregateIdentity identity, string projectionName)
        => state.LastDeliveredSequence == 0
            ? state.GuardEvidence is null && state.LastCompletedMessageId is null
                && state.CompletedReceipts is { Count: 0 } && state.CompletedMessageIdDigests is { Count: 0 }
                && ProjectionDeliveryIdempotencyCoordinator.IsValidCurrentState(state, identity, projectionName,
                    ProjectionDeliveryFingerprint.ComputeInitial(identity, projectionName,
                        state.CompletedPrefixFingerprint?.StartsWith("v2:", StringComparison.Ordinal) == true))
            : state.GuardEvidence is { } evidence && evidence.Source.SourceIncarnation == marker.SourceIncarnation
                && evidence.CatalogFingerprint == marker.CatalogFingerprint && evidence.HandlerSemantics == marker.HandlerSemantics
                && evidence.Source.HeadSequence == state.LastDeliveredSequence && evidence.Source.HeadMessageId == state.LastCompletedMessageId
                && evidence.OwnerProof.Selection.AppliedSequence == state.LastDeliveredSequence
                && ProjectionDeliveryIdempotencyCoordinator.ValidOwnerProof(evidence.OwnerProof, identity, projectionName)
                && evidence.IdleLifecycleRevision > 0;

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryReconciliationWork?> ReadReconciliationAsync(AggregateIdentity identity, string projectionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ProjectionKeySegments.Validate(projectionName, nameof(projectionName));
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ReconciliationReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> bytes, _) = await daprClient.GetByteStateAndETagAsync(projectionOptions.Value.CheckpointStateStoreName,
            ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName), cancellationToken: cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, bytes.Length);
        if (bytes.Length > 16_384) throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
        return bytes.IsEmpty ? null : JsonSerializer.Deserialize<ProjectionDeliveryReconciliationWork>(bytes.Span, daprClient.JsonSerializerOptions);
    }

    private byte[] SerializeWork(ProjectionDeliveryReconciliationWork work)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(work, daprClient.JsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSerializationBytes, bytes.Length);
        if (bytes.Length > 16_384) throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
        return bytes;
    }

    private async Task<(ReadOnlyMemory<byte> Body, string Etag)> ReadWriterBodyAsync(CancellationToken cancellationToken)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MarkerReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        (ReadOnlyMemory<byte> body, string etag) = await daprClient.GetByteStateAndETagAsync(
            projectionOptions.Value.CheckpointStateStoreName, ProjectionDeliveryStateKeys.WriterProtocol,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MarkerReadBytes, body.Length);
        if (body.Length > 4096) throw new JsonException("Invalid writer protocol record size.");
        return (body, etag);
    }

    private void ValidateSize(ProjectionDeliveryState state, int bytes)
        => ProjectionDeliveryStateSize.Validate(state, bytes, idempotencyOptions?.Value.CompletedReceiptLimit ?? 256);

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
        if ((state.SchemaVersion != ProjectionDeliveryState.CurrentSchemaVersion
                && state.SchemaVersion != ProjectionDeliveryState.GuardSchemaVersion)
            || state.WriterProtocolVersion != ProjectionDeliveryState.CurrentWriterProtocolVersion
            || !string.Equals(state.TenantId, identity.TenantId, StringComparison.Ordinal)
            || !string.Equals(state.Domain, identity.Domain, StringComparison.Ordinal)
            || !string.Equals(state.AggregateId, identity.AggregateId, StringComparison.Ordinal)
            || !string.Equals(state.ProjectionName, projectionName, StringComparison.Ordinal)) {
            throw new ArgumentException("Projection delivery state does not match the current writer and requested scope.", nameof(state));
        }
    }
}
