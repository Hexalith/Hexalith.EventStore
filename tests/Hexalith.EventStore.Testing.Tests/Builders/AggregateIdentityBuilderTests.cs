
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Testing.Builders;

namespace Hexalith.EventStore.Testing.Tests.Builders;

public class AggregateIdentityBuilderTests {
    [Fact]
    public void Build_produces_valid_aggregate_identity_with_defaults() {
        AggregateIdentity identity = new AggregateIdentityBuilder().Build();

        Assert.Equal(TestDataConstants.TenantId, identity.TenantId);
        Assert.Equal(TestDataConstants.Domain, identity.Domain);
        Assert.Equal(TestDataConstants.AggregateId, identity.AggregateId);
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
