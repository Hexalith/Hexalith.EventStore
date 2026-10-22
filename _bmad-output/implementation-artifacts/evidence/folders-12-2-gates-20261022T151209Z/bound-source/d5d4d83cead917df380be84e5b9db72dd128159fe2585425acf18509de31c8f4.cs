
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Testing.Builders;
/// <summary>
/// Fluent builder for creating <see cref="AggregateIdentity"/> instances with sensible defaults for testing.
/// </summary>
public sealed class AggregateIdentityBuilder {
    private string _tenantId = TestDataConstants.TenantId;
    private string _domain = TestDataConstants.Domain;
    private string _aggregateId = TestDataConstants.AggregateId;

    /// <summary>Sets the tenant identifier.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>This builder instance.</returns>
    public AggregateIdentityBuilder WithTenantId(string tenantId) { _tenantId = tenantId; return this; }

    /// <summary>Sets the domain name.</summary>
    /// <param name="domain">The domain name.</param>
    /// <returns>This builder instance.</returns>
    public AggregateIdentityBuilder WithDomain(string domain) { _domain = domain; return this; }

    /// <summary>Sets the aggregate identifier.</summary>
    /// <param name="aggregateId">The aggregate identifier.</param>
    /// <returns>This builder instance.</returns>
    public AggregateIdentityBuilder WithAggregateId(string aggregateId) { _aggregateId = aggregateId; return this; }

    /// <summary>Builds the <see cref="AggregateIdentity"/> instance.</summary>
    /// <returns>A new <see cref="AggregateIdentity"/> with the configured values.</returns>
    public AggregateIdentity Build() => new(_tenantId, _domain, _aggregateId);
}
