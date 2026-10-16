using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class NamedProjectionDispatchCoordinator
{
    private async Task<ProjectionDispatchResponse?> InvokeRecoveryBoundRoutesAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, ProjectionDeliveryRetryWorkItem work, IReadOnlyList<string> routes,
        ProjectionEventDto[] events, IReadOnlyDictionary<string, ProjectionDeliveryReservation> reservations,
        IReadOnlySet<string> completedRoutes, CancellationToken cancellationToken)
    {
        string[] bound = [.. work.ReplayRecoveryBindings!.Keys.Order(StringComparer.Ordinal)];
        var outcomes = new List<ProjectionDispatchOutcome>();
        if (bound.Any(routes.Contains))
        {
            // The original coordinated group remains frozen after individual row completions.
            // A blocked member prevents a first preparation; completed members reuse only the receipt.
            if (bound.Any(route => !routes.Contains(route) && !completedRoutes.Contains(route))) return null;
            if (events.Length != work.HeadSequence || events.Length > 10_000 || events[0].SequenceNumber != 1
                || events[^1].MessageId != work.DispatchId) return null;
            using (var size = new SizeLimitedWriteStream(67_108_864))
            {
                try { await ProjectionWireJsonSerializer.SerializeAsync(size, events, SerializerOptions, cancellationToken).ConfigureAwait(false); }
                catch (ProjectionRebuildPrefixSafetyLimitExceededException) { return null; }
                finally { Hexalith.EventStore.Client.Projections.ProjectionDeliveryCostDiagnostics.Record(
                    Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.WireSizingBytes, size.AttemptedBytes); }
            }
            string header = RecoveryHeader(work);
            if (System.Text.Encoding.UTF8.GetByteCount(header) > 4096) return null;
            var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, events)
            { RebuildSourceHeadSequence = work.HeadSequence, RebuildSourceHeadMessageId = work.HeadMessageId },
                bound, work.DispatchId, work.CatalogFingerprint);
            foreach (string method in new[] { "project/rebuild/stage/v1", "project/rebuild/commit/v1", "project/rebuild/verify/v1" })
            {
                ProjectionDispatchResponse? response = await InvokeAsync(catalog.AppId, method, request, options.Value,
                    cancellationToken, header).ConfigureAwait(false);
                if (response is null) return null;
                Dictionary<string, ProjectionDispatchOutcome> valid = ValidateOutcomes(response, bound, options.Value);
                if (bound.Any(route => !valid.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                    || outcome.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted))
                    return new(ProjectionDispatchProtocol.Version, [.. routes.Select(route => valid.GetValueOrDefault(route)
                        ?? new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Indeterminate, null, ProjectionDispatchReasonCodes.PartialRetry))]);
                if (method == "project/rebuild/verify/v1") outcomes.AddRange(bound.Where(routes.Contains).Select(route => valid[route]));
            }
        }
        string[] ordinary = [.. routes.Except(bound, StringComparer.Ordinal)];
        if (ordinary.Length > 0)
        {
            ProjectionDispatchResponse? compatible = await InvokeCompatibleRoutesAsync(identity, catalog, work, ordinary,
                events, reservations, cancellationToken).ConfigureAwait(false);
            if (compatible is null) return null;
            outcomes.AddRange(compatible.Outcomes);
        }
        return new(ProjectionDispatchProtocol.Version, outcomes);
    }

    private static string RecoveryHeader(ProjectionDeliveryRetryWorkItem work)
        => JsonSerializer.Serialize(work.ReplayRecoveryBindings!.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal), SerializerOptions);

    private async Task ReclaimCompletedRecoveryAsync(AggregateIdentity identity, NamedProjectionRouteCatalogEntry catalog,
        ProjectionDeliveryRetryWorkItem work, HashSet<string> completedRoutes, CancellationToken cancellationToken)
    {
        if (work.ReplayRecoveryBindings is not { Count: > 0 } bindings) return;
        string[] group = [.. bindings.Keys.Order(StringComparer.Ordinal)];
        if (group.All(completedRoutes.Contains))
        {
            var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, [])
                { RebuildSourceHeadSequence = work.HeadSequence, RebuildSourceHeadMessageId = work.HeadMessageId },
                group, work.DispatchId, work.CatalogFingerprint);
            ProjectionDispatchResponse? response = await InvokeAsync(catalog.AppId, "project/rebuild/reclaim-recovery/v1",
                request, options.Value, cancellationToken, RecoveryHeader(work)).ConfigureAwait(false);
            if (response is not null)
            {
                Dictionary<string, ProjectionDispatchOutcome> valid = ValidateOutcomes(response, group, options.Value);
                if (group.All(route => valid.TryGetValue(route, out ProjectionDispatchOutcome? outcome)
                    && outcome.Status is ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted)) return;
            }
        }
        // Keep the complete original group pending until every C and cleanup are proven.
        completedRoutes.ExceptWith(group);
    }
}
