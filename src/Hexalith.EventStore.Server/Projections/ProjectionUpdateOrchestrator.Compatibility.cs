using Hexalith.EventStore.ServiceDefaults.Routing;

using Dapr.Actors;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

internal partial class ProjectionUpdateOrchestrator
{
    private static bool IsUnavailableActorMethod(Exception failure)
    {
        // Dapr preserves the remote exception type separately from its message.
        // Neither a transient transport failure nor an arbitrary 404 is evidence
        // that the older actor lacks this additive method.
        for (int depth = 0; depth < 8 && failure is not null; depth++, failure = failure.InnerException!)
            if (failure is ActorInvokeException remote
                && remote.ActualExceptionType == typeof(MissingMethodException).FullName) return true;
        return false;
    }

    private async Task<bool> CanUseReleasedWriterAsync(AggregateIdentity identity, string route,
        ProjectionActivationWorkItem? sampledWork, CancellationToken cancellationToken)
    {
        ProjectionDeliveryStateReadResult scope = await RouteCatalogRemoteCall.InvokeAsync(() => deliveryStateStore!.ReadAsync(identity, route, cancellationToken)).ConfigureAwait(false);
        if (scope.WriterProtocolV2Active || scope.SourceGuard is not null
            || scope.Classification is not (ProjectionDeliveryStateClassification.Absent
                or ProjectionDeliveryStateClassification.LegacyZero or ProjectionDeliveryStateClassification.LegacyNonZero)
            || scope.State?.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion
            || scope.State?.GuardEvidence is not null || scope.State?.ActiveReservation is not null
            || scope.State?.IdleBindingPending is not null) return false;
        ProjectionActivationWorkItem? current = await RouteCatalogRemoteCall.InvokeAsync(() => projectionActivationOutbox!.GetAsync(identity, cancellationToken)).ConfigureAwait(false);
        return sampledWork?.LegacyDelivery is null && current?.LegacyDelivery is null
            && current?.Revision == sampledWork?.Revision;
    }
}
