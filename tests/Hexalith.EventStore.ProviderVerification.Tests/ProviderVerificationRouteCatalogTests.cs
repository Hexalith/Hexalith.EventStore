using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

/// <summary>Verifies the synthetic catalog cannot authorize other routes or tenant scopes.</summary>
public sealed class ProviderVerificationRouteCatalogTests
{
    [Theory]
    [InlineData("query-fresh-data", "tenant-contract-a", "tenant-contract-case")]
    [InlineData("query-auth-tenant", "tenant-contract-case", "tenant-contract-a")]
    public async Task ActiveFixtureProvidesValidatedSchemaThreeWithOneExactRouteAndTenant(
        string state, string tenant, string otherTenant)
    {
        var coordinator = new ProviderStateCoordinator(new HashSet<string>([state], StringComparer.Ordinal));
        coordinator.BeginInteraction(state);
        (await coordinator.ApplyAsync(state, "setup", TestContext.Current.CancellationToken)).ShouldBe("state.setup.succeeded");
        var authority = new ProviderVerificationRouteCatalog(coordinator);
        await using RouteCatalogDispatchLease lease = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        CommittedRouteCatalogSnapshot snapshot = lease.Snapshot;

        snapshot.Envelope.SchemaVersion.ShouldBe(3);
        snapshot.Envelope.Operations.ShouldHaveSingleItem().Eligibility.ShouldBe("UI-only");
        snapshot.Envelope.Routes.ShouldHaveSingleItem().ResolverTenantPattern.ShouldBe(tenant);
        RouteCatalogValidator.Validate(snapshot.Envelope);
        snapshot.RootDigest.ShouldBe(RouteCatalogCodec.ComputeRootDigest(snapshot.Envelope));
        snapshot.Resolve("query", tenant, "orders", "GetOrders").Declaration.ProjectionType.ShouldBe("orders");
        Should.Throw<RouteCatalogUnavailableException>(() => snapshot.Resolve("query", otherTenant, "orders", "GetOrders"));
        Should.Throw<RouteCatalogUnavailableException>(() => snapshot.Resolve("query", tenant, "orders", "UndeclaredQuery"));
        Should.Throw<RouteCatalogUnavailableException>(() => snapshot.Resolve("command", tenant, "orders", "GetOrders"));
        Should.Throw<RouteCatalogUnavailableException>(() => snapshot.Resolve("query", tenant, "other-domain", "GetOrders"));

        RouteCatalogGenerationReadback readback = await authority.ReadbackAsync(TestContext.Current.CancellationToken);
        readback.RootDigest.ShouldBe(snapshot.RootDigest);
        readback.Serving.ShouldBeTrue();
        (await coordinator.ApplyAsync(state, "teardown", TestContext.Current.CancellationToken)).ShouldBe("state.teardown.succeeded");
    }

    [Fact]
    public async Task SingletonAuthorityChangesExactTenantWithStateAndRefusesAcquisitionAfterTeardown()
    {
        var coordinator = new ProviderStateCoordinator(new HashSet<string>(
            ["query-fresh-data", "query-auth-tenant"], StringComparer.Ordinal));
        var authority = new ProviderVerificationRouteCatalog(coordinator);
        coordinator.BeginInteraction("query-fresh-data");
        (await coordinator.ApplyAsync("query-fresh-data", "setup", TestContext.Current.CancellationToken))
            .ShouldBe("state.setup.succeeded");
        await using RouteCatalogDispatchLease ordinary = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        ordinary.Snapshot.Resolve("query", "tenant-contract-a", "orders", "GetOrders").Target.ResolverTenantPattern
            .ShouldBe("tenant-contract-a");
        Should.Throw<RouteCatalogUnavailableException>(() =>
            ordinary.Snapshot.Resolve("query", "tenant-contract-case", "orders", "GetOrders"));
        (await coordinator.ApplyAsync("query-fresh-data", "teardown", TestContext.Current.CancellationToken))
            .ShouldBe("state.teardown.succeeded");
        await Should.ThrowAsync<InvalidOperationException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));

        coordinator.BeginInteraction("query-auth-tenant");
        (await coordinator.ApplyAsync("query-auth-tenant", "setup", TestContext.Current.CancellationToken))
            .ShouldBe("state.setup.succeeded");
        await using RouteCatalogDispatchLease propagated = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        propagated.Snapshot.Resolve("query", "tenant-contract-case", "orders", "GetOrders").Target.ResolverTenantPattern
            .ShouldBe("tenant-contract-case");
        Should.Throw<RouteCatalogUnavailableException>(() =>
            propagated.Snapshot.Resolve("query", "tenant-contract-a", "orders", "GetOrders"));
        propagated.Snapshot.RootDigest.ShouldNotBe(ordinary.Snapshot.RootDigest);
        authority.AcquisitionCount.ShouldBe(2);
        (await coordinator.ApplyAsync("query-auth-tenant", "teardown", TestContext.Current.CancellationToken))
            .ShouldBe("state.teardown.succeeded");
        coordinator.CurrentState.ShouldBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        authority.AcquisitionCount.ShouldBe(2);
    }

    [Fact]
    public async Task LifecycleMutationsAreRefusedWithoutChangingAcquiredInventory()
    {
        const string state = "query-fresh-data";
        var coordinator = new ProviderStateCoordinator(new HashSet<string>([state], StringComparer.Ordinal));
        coordinator.BeginInteraction(state);
        (await coordinator.ApplyAsync(state, "setup", TestContext.Current.CancellationToken)).ShouldBe("state.setup.succeeded");
        var authority = new ProviderVerificationRouteCatalog(coordinator);
        await using RouteCatalogDispatchLease original = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        byte[] originalBytes = original.Snapshot.GetCanonicalBytes();
        var candidate = new RouteCatalogActivation("unapproved-attempt", originalBytes,
            original.Snapshot.RootDigest, null, [], "provider-test", string.Empty);

        await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
            authority.PrepareAsync(candidate, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
            authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
            authority.CommitAsync(candidate.AttemptId, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
            authority.RollbackAsync(new(candidate.AttemptId, "unapproved-binding", 1, "pending"), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
            authority.ReconcileAsync(new("unapproved-lease", original.Snapshot.RootDigest, 1, "unapproved-proof"),
                TestContext.Current.CancellationToken));

        await using RouteCatalogDispatchLease unchanged = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        unchanged.Snapshot.GetCanonicalBytes().ShouldBe(originalBytes);
        unchanged.Snapshot.RootDigest.ShouldBe(original.Snapshot.RootDigest);
        unchanged.Snapshot.Envelope.Routes.ShouldHaveSingleItem().ResolverTenantPattern.ShouldBe("tenant-contract-a");
        unchanged.Snapshot.Resolve("query", "tenant-contract-a", "orders", "GetOrders").Declaration.ProjectionType.ShouldBe("orders");
        (await coordinator.ApplyAsync(state, "teardown", TestContext.Current.CancellationToken)).ShouldBe("state.teardown.succeeded");
    }
}
