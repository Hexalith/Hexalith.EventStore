using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Actors;

public partial class EventReplayProjectionActor
{
    private bool _stagedModelChanged;
    private ProjectionState? _stagedModel;
    private bool _stagedReceiptChanged;
    private ProjectionRebuildPromotionReceipt? _stagedReceipt;

    private async Task<ConditionalValue<T>> ReadPublicationStateAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
        try
        {
            ConditionalValue<T> result = await StateManager.TryGetStateAsync<T>(key, cancellationToken).ConfigureAwait(false);
            if (result.HasValue) MeasurePublicationBody(result.Value, writing: false);
            return result;
        }
        catch
        {
            await ClearPublicationCacheAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task SetPublicationStateAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        MeasurePublicationBody(value, writing: true);
        if (key == ProjectionStateKey) { _stagedModelChanged = true; _stagedModel = (ProjectionState)(object)value!; }
        if (key == ProjectionRebuildPromotionReceiptKey)
        { _stagedReceiptChanged = true; _stagedReceipt = (ProjectionRebuildPromotionReceipt)(object)value!; }
        try { await StateManager.SetStateAsync(key, value, cancellationToken).ConfigureAwait(false); }
        catch { await ClearPublicationCacheAsync().ConfigureAwait(false); throw; }
    }

    private async Task RemovePublicationStateAsync(string key, CancellationToken cancellationToken = default)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        if (key == ProjectionStateKey) { _stagedModelChanged = true; _stagedModel = null; }
        if (key == ProjectionRebuildPromotionReceiptKey) { _stagedReceiptChanged = true; _stagedReceipt = null; }
        try { await StateManager.RemoveStateAsync(key, cancellationToken).ConfigureAwait(false); }
        catch { await ClearPublicationCacheAsync().ConfigureAwait(false); throw; }
    }

    private async Task<bool> TryRemovePublicationStateAsync(string key, CancellationToken cancellationToken = default)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
        try
        {
            bool removed = await StateManager.TryRemoveStateAsync(key, cancellationToken).ConfigureAwait(false);
            if (removed) ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
            return removed;
        }
        catch { await ClearPublicationCacheAsync().ConfigureAwait(false); throw; }
    }

    private async Task SavePublicationStateAsync(CancellationToken cancellationToken = default)
    {
        bool modelChanged = _stagedModelChanged, receiptChanged = _stagedReceiptChanged;
        ProjectionState? expectedModel = _stagedModel;
        ProjectionRebuildPromotionReceipt? expectedReceipt = _stagedReceipt;
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.TransactionAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        try
        {
            await StateManager.SaveStateAsync(cancellationToken).ConfigureAwait(false);
            if (modelChanged) ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelMutations);
        }
        catch
        {
            await ClearPublicationCacheAsync().ConfigureAwait(false);
            // Count a lost committed response only after the owning persisted
            // receipt AND selected state prove the attempted immutable transition.
            if (modelChanged && receiptChanged)
            {
                try
                {
                    ConditionalValue<ProjectionRebuildPromotionReceipt> receipt = await ReadPublicationStateAsync<ProjectionRebuildPromotionReceipt>(
                        ProjectionRebuildPromotionReceiptKey).ConfigureAwait(false);
                    ConditionalValue<ProjectionState> model = await ReadPublicationStateAsync<ProjectionState>(ProjectionStateKey).ConfigureAwait(false);
                    if ((expectedReceipt is null ? !receipt.HasValue : receipt.HasValue && SamePublicationReceipt(receipt.Value, expectedReceipt))
                        && (expectedModel is null ? !model.HasValue : model.HasValue && SamePublicationState(model.Value, expectedModel)))
                        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelMutations);
                }
                catch { /* Failed readback remains unresolved; mutation attempts are already charged. */ }
            }
            throw;
        }
        finally { ResetPublicationMeasurements(); }
    }

    private async Task ClearPublicationCacheAsync()
    {
        ResetPublicationMeasurements();
        await StateManager.ClearCacheAsync().ConfigureAwait(false);
    }

    private void ResetPublicationMeasurements()
    {
        _stagedModelChanged = false; _stagedModel = null;
        _stagedReceiptChanged = false; _stagedReceipt = null;
    }

    private static bool SamePublicationState(ProjectionState? actual, ProjectionState? expected)
        => actual is null ? expected is null : expected is not null && actual.TenantId == expected.TenantId
            && actual.ProjectionType == expected.ProjectionType && actual.StateBytes.AsSpan().SequenceEqual(expected.StateBytes);

    private static bool SamePublicationReceipt(ProjectionRebuildPromotionReceipt actual, ProjectionRebuildPromotionReceipt expected)
        => actual.OperationId == expected.OperationId && actual.Completed == expected.Completed
            && actual.PublicationBinding == expected.PublicationBinding && actual.PublicationFloor == expected.PublicationFloor
            && actual.SelectedReplayFence == expected.SelectedReplayFence && actual.PriorReplayFence == expected.PriorReplayFence
            && actual.SelectedStateFingerprint == expected.SelectedStateFingerprint
            && SamePublicationState(actual.PromotedState, expected.PromotedState)
            && SamePublicationState(actual.PreviousState, expected.PreviousState);

    private static void MeasurePublicationBody<T>(T value, bool writing)
    {
        using var size = new SizeLimitedWriteStream(long.MaxValue);
        try { JsonSerializer.Serialize(size, value, JsonSerializerOptions.Web); }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, size.AttemptedBytes); }
        bool controlOnly = value is ProjectionRebuildPromotionReceipt { Completed: true };
        if (writing) ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, size.Length);
        else if (controlOnly) ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, size.Length);
        if (!controlOnly) ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ModelBytes, size.Length);
    }
}
