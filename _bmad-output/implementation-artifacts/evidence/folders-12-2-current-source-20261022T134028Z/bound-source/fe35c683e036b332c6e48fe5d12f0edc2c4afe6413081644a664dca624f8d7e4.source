namespace Hexalith.EventStore.Client.Projections;

public sealed partial class SharedProjectionEpochCoordinator
{
    /// <summary>
    /// Inspects the existing journal checkpoint and terminal fold receipt without registration,
    /// catch-up, control-index reconciliation, model reads or repair. A journal acknowledgement alone
    /// never proves application. Whole-family selected-owner proof and lifecycle binding remain separate.
    /// </summary>
    /// <param name="scope">The exact registered tenant/global family and complete required writer inventory.</param>
    /// <param name="delivery">The immutable original source sequence, position, payload and checkpoint.</param>
    /// <param name="cancellationToken">Stops the next proof read.</param>
    /// <returns>Per-source applied evidence at one unchanged selected epoch.</returns>
    public async Task<SharedProjectionAppliedDeliveryProof> ReadAppliedDeliveryProofAsync(SharedProjectionScope scope,
        SharedProjectionDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(delivery);
        scope.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(delivery.SourceStream);
        if (delivery.OwnerSequenceNumber <= 0 || delivery.EnvelopePosition <= 0
            || delivery.CanonicalPayload is null || delivery.Checkpoint is null)
            throw new ArgumentException("Applied proof requires the original positive source identity.", nameof(delivery));
        ReadModelEntry<SharedProjectionEpochState> before = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
        if (before.Value is not { } state) return new(false, "delivery_rebuild_required", 0, 0, null, null);
        ValidateState(state);
        ValidateBoundary(state, scope);
        if (state.Phase != SharedProjectionEpochPhase.Open || state.OwnerSequenceHandoffPending
            || state.OffboardingAuditId is not null || state.Journal.Length != 0 || HasConflictReadinessIssue(state)
            || scope.RequiredWriters.Any(writer => !state.RegisteredWriters.Contains(writer, StringComparer.Ordinal)))
            return new(false, "delivery_in_progress", state.Epoch, state.ActiveGeneration, null, null);
        SharedProjectionCheckpoint? checkpoint = await ReadOwnerCheckpointAsync(scope, state, delivery.SourceStream,
            cancellationToken).ConfigureAwait(false);
        if (checkpoint is null || checkpoint.OwnerSequenceNumber < delivery.OwnerSequenceNumber
            || checkpoint.Position < delivery.EnvelopePosition)
            return new(false, "delivery_reconciliation_required", state.Epoch, state.ActiveGeneration, null, null);
        ReadModelEntry<SharedProjectionDeliveryReceipt> receipt = await _store.GetAsync<SharedProjectionDeliveryReceipt>(scope.StoreName,
            scope.ReceiptKey(state.Epoch, delivery.SourceStream, delivery.EnvelopePosition), cancellationToken).ConfigureAwait(false);
        if (receipt.Value is null) return new(false, "delivery_rebuild_required", state.Epoch, state.ActiveGeneration, null, null);
        string digest = ComputeDeliveryDigest(delivery);
        if (receipt.Value.Parked || receipt.Value.Failure is not null || receipt.Value.Digest != digest)
            return new(false, "delivery_identity_conflict", state.Epoch, state.ActiveGeneration, null, null);
        ReadModelEntry<SharedProjectionEpochState> after = await ReadStateAsync(scope, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(before.ETag) || before.ETag != after.ETag)
            return new(false, "head_changed", state.Epoch, state.ActiveGeneration, null, null);
        // Return the opaque receipt identity rather than its private state key or payload.
        string identity = state.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
            + HashIdentity(delivery.SourceStream, delivery.EnvelopePosition);
        return new(true, null, state.Epoch, state.ActiveGeneration, identity, digest);
    }
}
