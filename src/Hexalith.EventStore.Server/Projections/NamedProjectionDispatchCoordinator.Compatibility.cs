using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

internal sealed partial class NamedProjectionDispatchCoordinator
{
    private async Task<ProjectionDispatchResponse?> InvokeCompatibleRoutesAsync(AggregateIdentity identity,
        NamedProjectionRouteCatalogEntry catalog, ProjectionDeliveryRetryWorkItem work,
        IReadOnlyList<string> routes, ProjectionEventDto[] events,
        IReadOnlyDictionary<string, ProjectionDeliveryReservation> reservations, CancellationToken cancellationToken)
    {
        var outcomes = new List<ProjectionDispatchOutcome>();
        foreach (string route in routes)
        {
            bool perEvent = catalog.SingleEventProjectionTypes.Contains(route, StringComparer.Ordinal)
                || catalog.IncrementalReplayProjectionTypes.Contains(route, StringComparer.Ordinal);
            ProjectionEventDto[][] calls = perEvent
                ? [.. events.Where(item => item.SequenceNumber >= reservations[route].StartSequence).Select(static item => new[] { item })]
                : [events];
            ProjectionDispatchOutcome? terminal = null;
            foreach (ProjectionEventDto[] call in calls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, call),
                    [route], work.DispatchId, catalog.CatalogFingerprint);
                ProjectionDispatchResponse? response = await InvokeAsync(catalog.AppId, "project/v2", request,
                    options.Value, cancellationToken).ConfigureAwait(false);
                if (response is null) return null;
                Dictionary<string, ProjectionDispatchOutcome> valid = ValidateOutcomes(response, [route], options.Value);
                if (!valid.TryGetValue(route, out terminal)) return null;
                if (terminal.Status is not ProjectionDispatchStatus.Completed and not ProjectionDispatchStatus.AlreadyCompleted) break;
            }
            outcomes.Add(terminal ?? new(route, ProjectionDispatchStatus.Indeterminate, null,
                ProjectionDispatchReasonCodes.MalformedOutcome));
        }
        return new(ProjectionDispatchProtocol.Version, outcomes);
    }
}
