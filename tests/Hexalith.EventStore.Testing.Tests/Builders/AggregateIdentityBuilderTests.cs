namespace Hexalith.EventStore.Testing.Tests.Builders;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Testing.Builders;

public class AggregateIdentityBuilderTests {
    [Fact]
    public void Build_produces_valid_aggregate_identity_with_defaults() {
        AggregateIdentity identity = new AggregateIdentityBuilder().Build();

        Assert.Equal("test-tenant", identity.TenantId);
        Assert.Equal("test-domain", identity.Domain);
        Assert.Equal("test-agg-001", identity.AggregateId);
    }

    [Fact]
    public void Build_fluent_overrides_work() {
        AggregateIdentity identity = new AggregateIdentityBuilder()
            .WithTenantId("acme")
            .WithDomain("billing")
            .WithAggregateId("inv-001")
            .Build();

        Assert.Equal("acme", identity.TenantId);
        Assert.Equal("billing", identity.Domain);
        Assert.Equal("inv-001", identity.AggregateId);
    }
}
