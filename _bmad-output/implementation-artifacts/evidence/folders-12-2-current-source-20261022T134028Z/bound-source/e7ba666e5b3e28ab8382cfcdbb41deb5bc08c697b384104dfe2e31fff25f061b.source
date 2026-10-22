using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Domains whose source writes are covered by the mandatory shared-rebuild roster.</summary>
public sealed class SharedProjectionSourceCaptureOptions
{
    /// <summary>Gets or sets exact domain names that require installed roster registration.</summary>
    public string[] EnabledDomains { get; set; } = [];

    /// <summary>Whether this domain requires registration before any event persistence.</summary>
    public bool RequiresRegistration(string domain)
        => EnabledDomains.Contains(domain, StringComparer.Ordinal);
}

/// <summary>Obtains a durable source write intent before an aggregate appends events.</summary>
public interface ISharedProjectionSourceWriteFence
{
    /// <summary>Registers the owner and begins one protected event persistence attempt.</summary>
    Task BeginWriteAsync(AggregateIdentity identity, string intentId, bool isErasure = false,
        CancellationToken cancellationToken = default);

    /// <summary>Settles the intent after source event persistence has a resolved outcome.</summary>
    Task CompleteWriteAsync(AggregateIdentity identity, string intentId, CancellationToken cancellationToken = default);
}

/// <summary>Dapr actor-backed source write registration; an uninstalled roster rejects writes.</summary>
public sealed class DaprSharedProjectionSourceWriteFence(IActorProxyFactory proxies)
    : ISharedProjectionSourceWriteFence
{
    /// <inheritdoc/>
    public async Task BeginWriteAsync(AggregateIdentity identity, string intentId, bool isErasure = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identity);
        SharedProjectionSourceScope scope = new(identity.TenantId, identity.Domain);
        ISharedProjectionSourceRosterActor actor = Proxy(scope);
        if (!await actor.BeginWriteAsync(identity, intentId, isErasure).ConfigureAwait(false))
            throw new InvalidOperationException("Shared projection source roster rejected this write.");
    }

    /// <inheritdoc/>
    public async Task CompleteWriteAsync(AggregateIdentity identity, string intentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identity);
        SharedProjectionSourceScope scope = new(identity.TenantId, identity.Domain);
        if (!await Proxy(scope).CompleteWriteAsync(identity, intentId).ConfigureAwait(false))
            throw new InvalidOperationException("Shared projection source write intent could not be settled.");
    }

    private ISharedProjectionSourceRosterActor Proxy(SharedProjectionSourceScope scope)
        => proxies.CreateActorProxy<ISharedProjectionSourceRosterActor>(
            new ActorId(scope.ActorId), SharedProjectionSourceRosterActor.ActorTypeName);
}
