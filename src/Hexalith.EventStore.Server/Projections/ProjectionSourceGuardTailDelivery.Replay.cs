using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class ProjectionSourceGuardTailDelivery
{
    private async Task<string?> ApplyCanonicalReplayAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        IReadOnlyList<ProjectionSourceGuardTailPlan> plans, IReadOnlyDictionary<string, ProjectionLifecycleSnapshot> snapshots,
        string dispatchId, CancellationToken cancellationToken)
    {
        ProjectionSourceGuardTailPlan[] replays = [.. plans.Where(static plan => plan.ReplayEvents is not null)];
        ProjectionEventDto[] events = replays[0].ReplayEvents!;
        // Each route admitted the same frozen complete source. Its readable bytes are retained;
        // no partial page or application reread is substituted for this immutable prefix.
        var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, events)
        {
            RebuildSourceHeadSequence = replays[0].Binding.AdmittedSource.HeadSequence,
            RebuildSourceHeadMessageId = replays[0].Binding.AdmittedSource.HeadMessageId,
            RebuildOwnerGenerations = replays.ToDictionary(static plan => plan.Projection,
                static plan => plan.Binding.SelectedOwnerGeneration, StringComparer.Ordinal),
            RebuildOwnerIdentities = replays.ToDictionary(static plan => plan.Projection,
                plan => plan.ObservedOwner?.Selection.OwnerIdentity ?? identity.AggregateId, StringComparer.Ordinal),
        }, [.. replays.Select(static plan => plan.Projection)], dispatchId, catalog.CatalogFingerprint);
        ProjectionDispatchRequest? prepared = await transport.PrepareReplayRequestAsync(catalog, request, cancellationToken).ConfigureAwait(false);
        if (prepared is null) return "delivery_reconciliation_required";
        foreach (ProjectionMappedReplayAction action in new[] { ProjectionMappedReplayAction.Stage,
            ProjectionMappedReplayAction.Commit, ProjectionMappedReplayAction.Verify })
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (ProjectionSourceGuardTailPlan plan in replays)
                if (await lifecycle.ReadSnapshotAsync(identity, plan.Projection, cancellationToken).ConfigureAwait(false) != snapshots[plan.Projection])
                    return "delivery_in_progress";
            ProjectionDispatchResponse? result = await transport.InvokeReplayAsync(catalog, prepared, action, cancellationToken).ConfigureAwait(false);
            if (result is not { Version: ProjectionDispatchProtocol.Version } || result.Outcomes.Count != replays.Length
                || result.Outcomes.Select(static outcome => outcome.ProjectionType).Distinct(StringComparer.Ordinal).Count() != replays.Length
                || result.Outcomes.Any(outcome => outcome.State is not null
                    || !request.ProjectionTypes.Contains(outcome.ProjectionType, StringComparer.Ordinal)
                    || outcome.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted))
                return result?.Outcomes.FirstOrDefault(static outcome => outcome.ReasonCode is not null)?.ReasonCode switch
                {
                    ProjectionDispatchReasonCodes.UnsupportedCapability => ProjectionDispatchReasonCodes.UnsupportedCapability,
                    ProjectionDispatchReasonCodes.DeliveryIdentityConflict => ProjectionDispatchReasonCodes.DeliveryIdentityConflict,
                    ProjectionDispatchReasonCodes.DeliveryRebuildRequired => ProjectionDispatchReasonCodes.DeliveryRebuildRequired,
                    ProjectionDispatchReasonCodes.MalformedOutcome => ProjectionDispatchReasonCodes.MalformedOutcome,
                    ProjectionDispatchReasonCodes.DeliveryStateUnavailable => ProjectionDispatchReasonCodes.DeliveryStateUnavailable,
                    "delivery_proof_limit_exceeded" => "delivery_proof_limit_exceeded",
                    _ => "delivery_reconciliation_required",
                };
        }
        return null;
    }
}
