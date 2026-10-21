using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Actors;

public partial class EventReplayProjectionActor
{
    private async Task NotifyProjectionHintsAsync(ProjectionState state, CancellationToken cancellationToken = default)
    {
        // Preserve the legacy signal independently of whether the domain opted into additive scoped detail.
        await projectionChangeNotifier.NotifyProjectionChangedAsync(
            state.ProjectionType, state.TenantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (state.NotificationMetadata is not null || state.NotificationGroupScope is not null)
        {
            await projectionChangeNotifier.NotifyProjectionChangedAsync(new ProjectionChangedDetail(
                state.ProjectionType, state.TenantId, state.NotificationGroupScope,
                state.NotificationMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
