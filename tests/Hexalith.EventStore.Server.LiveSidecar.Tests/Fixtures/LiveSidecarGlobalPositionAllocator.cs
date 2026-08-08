using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Events;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Delegates to the production global-position actor while exposing an opt-in, test-only race gate.
/// </summary>
internal sealed class LiveSidecarGlobalPositionAllocator(
    IActorProxyFactory actorProxyFactory,
    AppendDurabilityRaceControl raceControl) : IGlobalPositionAllocator
{
    private readonly DaprGlobalPositionAllocator _inner = new(actorProxyFactory);

    /// <summary>Gets the production allocator type decorated by this test-only gate.</summary>
    public static string ProductionAllocatorTypeName => typeof(DaprGlobalPositionAllocator).FullName!;

    /// <inheritdoc/>
    public async Task<long> AllocateAsync(int count, CancellationToken cancellationToken = default)
    {
        AppendDurabilityRaceSession? session = raceControl.GetActiveSession();
        if (session is not null)
        {
            await session.InterceptAllocationAsync(cancellationToken).ConfigureAwait(false);
        }

        return await _inner.AllocateAsync(count, cancellationToken).ConfigureAwait(false);
    }
}
