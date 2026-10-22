using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Actual private DAPR transport for the candidate registry; no production registration, ACL or profile acceptance.</summary>
/// <param name="proxyFactory">Already configured private actor transport.</param>
/// <param name="timeProvider">Monotonic operational clock.</param>
public sealed class DaprInteractionOccurrenceRegistry(IActorProxyFactory proxyFactory, TimeProvider timeProvider) : IInteractionOccurrenceRegistry
{
    /// <inheritdoc/>
    public Task<InteractionOccurrenceReservationResult> ReserveAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); return InvokeAsync(request.Identity, proxy => proxy.ReserveAsync(request), cancellationToken);
    }
    /// <inheritdoc/>
    public Task<InteractionOccurrenceReservationResult> RetainSealedAsync(InteractionOccurrenceIdentity identity, InteractionOccurrenceSealedResult result, CancellationToken cancellationToken = default)
        => InvokeAsync(identity, proxy => proxy.RetainSealedAsync(identity, result), cancellationToken);
    /// <inheritdoc/>
    public Task<InteractionOccurrenceReservationResult> CompleteWriterAsync(InteractionOccurrenceIdentity identity, string keyReference, string proofId, bool persisted, CancellationToken cancellationToken = default)
        => InvokeAsync(identity, proxy => proxy.CompleteWriterAsync(identity, keyReference, proofId, persisted), cancellationToken);
    /// <inheritdoc/>
    public Task<InteractionOccurrenceReservationResult> LookupAsync(InteractionOccurrenceIdentity identity, CancellationToken cancellationToken = default)
        => InvokeAsync(identity, proxy => proxy.LookupAsync(identity), cancellationToken);

    private async Task<InteractionOccurrenceReservationResult> InvokeAsync(InteractionOccurrenceIdentity identity,
        Func<IInteractionOccurrenceRegistryActor, Task<InteractionOccurrenceReservationResult>> operation, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(identity.Target);
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), timeProvider, token, timeProvider.GetTimestamp());
        try
        {
            var result = await deadline.ReadAsync(_ => operation(proxyFactory.CreateActorProxy<IInteractionOccurrenceRegistryActor>(
                new ActorId(InteractionOccurrenceRegistryActor.GetActorId(identity.Target.TenantId)), InteractionOccurrenceRegistryActor.ActorTypeName))).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested(); return result;
        }
        catch (Exception) { token.ThrowIfCancellationRequested(); throw; }
    }
}
