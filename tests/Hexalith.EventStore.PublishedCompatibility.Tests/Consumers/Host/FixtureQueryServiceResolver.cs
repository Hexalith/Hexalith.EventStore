#if SELECTED
using Hexalith.EventStore.Server.DomainServices;

namespace PublishedConsumer;

/// <summary>Resolves the fixture's actual independently hosted Dapr domain-service application.</summary>
public sealed class FixtureQueryServiceResolver : IDomainServiceResolver
{
    /// <inheritdoc/>
    public Task<DomainServiceRegistration?> ResolveAsync(string tenantId, string domain, string version = "v1", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<DomainServiceRegistration?>(new(Environment.GetEnvironmentVariable("P1R_DOMAIN_APP_ID")!, "process", tenantId, domain, version));
    }
}
#endif
