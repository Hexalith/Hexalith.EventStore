using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private transport to the existing atomic protection owner, using a dedicated authenticated actor client.</summary>
/// <param name="proxies">Dedicated registrar-only actor transport; generic actor credentials are insufficient.</param>
/// <remarks>No DI or public endpoint registration is provided. The actor independently authenticates every operation/envelope.</remarks>
public sealed class DaprDeletionCapabilityCompromiseRegistrar(IActorProxyFactory proxies) : IDeletionCapabilityCompromiseRegistrar
{
    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> RegisterAsync(DeletionCapabilityRevocationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(envelope);
        var actor = proxies.CreateActorProxy<IDeletionConsumptionActor>(new(DeletionConsumptionActor.GetActorId(envelope.TenantId)),
            DeletionConsumptionActor.ActorTypeName);
        var result = await actor.RegisterRevocationAsync(envelope).WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested(); return result;
    }

    /// <inheritdoc/>
    public async Task<DeletionCapabilityRevocationReceipt?> LookupAsync(DeletionCapabilityRevocationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(envelope);
        var actor = proxies.CreateActorProxy<IDeletionConsumptionActor>(new(DeletionConsumptionActor.GetActorId(envelope.TenantId)),
            DeletionConsumptionActor.ActorTypeName);
        var result = await actor.LookupRevocationAsync(envelope).WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested(); return result;
    }
}
