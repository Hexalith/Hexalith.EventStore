using System.Security.Claims;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class StatefulProviderDependenciesTests
{
    [Fact]
    public async Task SupportedStateRegistry_AllNineteenStatesHaveExplicitRuntimeSeams()
    {
        SupportedProviderStates.All.Count.ShouldBe(19);
        foreach (string state in SupportedProviderStates.All.Order(StringComparer.Ordinal))
        {
            ProviderStateCoordinator coordinator = await ActiveCoordinatorAsync(state);
            if (state.StartsWith("command-", StringComparison.Ordinal) || state == "tenant-mismatch")
            {
                var router = new StatefulCommandRouter(coordinator);
                if (state == "command-unexpected-5xx")
                {
                    await Should.ThrowAsync<InvalidOperationException>(
                        () => router.RouteCommandAsync(Command(), TestContext.Current.CancellationToken));
                }
                else
                {
                    _ = await router.RouteCommandAsync(Command(), TestContext.Current.CancellationToken);
                }
            }
            else
            {
                var router = new StatefulQueryRouter(coordinator);
                if (state == "query-rate-limited")
                {
                    await Should.ThrowAsync<BackpressureExceededException>(
                        () => router.RouteQueryAsync(Query(), TestContext.Current.CancellationToken));
                }
                else
                {
                    _ = await router.RouteQueryAsync(Query(), TestContext.Current.CancellationToken);
                }

                _ = await new StatefulETagService(coordinator).GetCurrentETagAsync(
                    "orders",
                    "tenant-contract-a",
                    TestContext.Current.CancellationToken);
            }
        }
    }

    [Fact]
    public async Task AuthorizationValidator_RequiresExactClaimsAndRequestedValues()
    {
        ProviderStateCoordinator coordinator = await ActiveCoordinatorAsync("command-accepted");
        var validator = new StatefulAuthorizationValidator(coordinator);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-contract-a"),
            new Claim("eventstore:tenant", "tenant-contract-a"),
            new Claim("eventstore:domain", "orders"),
            new Claim("eventstore:permission", "command:*"),
        ], "test"));

        TenantValidationResult tenant = await validator.ValidateAsync(
            principal,
            "tenant-contract-a",
            TestContext.Current.CancellationToken,
            "order-1");
        RbacValidationResult rbac = await validator.ValidateAsync(
            principal,
            "tenant-contract-a",
            "orders",
            "Contracts+ShipOrderCommand",
            "command",
            TestContext.Current.CancellationToken,
            "order-1");
        RbacValidationResult wrongDomain = await validator.ValidateAsync(
            principal,
            "tenant-contract-a",
            "other",
            "Contracts+ShipOrderCommand",
            "command",
            TestContext.Current.CancellationToken,
            "order-1");
        RbacValidationResult wrongMessage = await validator.ValidateAsync(
            principal,
            "tenant-contract-a",
            "orders",
            "OtherCommand",
            "command",
            TestContext.Current.CancellationToken,
            "order-1");

        tenant.IsAuthorized.ShouldBeTrue();
        rbac.IsAuthorized.ShouldBeTrue();
        wrongDomain.IsAuthorized.ShouldBeFalse();
        wrongMessage.IsAuthorized.ShouldBeFalse();
    }

    [Fact]
    public async Task CommandRouter_ConflictState_ReturnsDeterministicFailure()
    {
        ProviderStateCoordinator coordinator = await ActiveCoordinatorAsync("command-conflict");
        var router = new StatefulCommandRouter(coordinator);

        CommandProcessingResult result = await router.RouteCommandAsync(Command(), TestContext.Current.CancellationToken);

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldBe("ConcurrencyConflict");
    }

    [Fact]
    public async Task QueryRouter_RateLimitedState_ThrowsBackpressureFailure()
    {
        ProviderStateCoordinator coordinator = await ActiveCoordinatorAsync("query-rate-limited");
        var router = new StatefulQueryRouter(coordinator);

        await Should.ThrowAsync<BackpressureExceededException>(
            () => router.RouteQueryAsync(Query(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task QueryRouter_FreshState_ReturnsProjectionBackedPayload()
    {
        ProviderStateCoordinator coordinator = await ActiveCoordinatorAsync("query-fresh-data");
        var router = new StatefulQueryRouter(coordinator);

        var result = await router.RouteQueryAsync(Query(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Metadata!.Provenance.ShouldBe(QueryResponseProvenance.ProjectionBacked);
        result.Payload!.Value.GetArrayLength().ShouldBe(1);
    }

    private static async Task<ProviderStateCoordinator> ActiveCoordinatorAsync(string state)
    {
        var coordinator = new ProviderStateCoordinator(new HashSet<string>([state], StringComparer.Ordinal));
        coordinator.BeginInteraction(state);
        _ = await coordinator.ApplyAsync(state, "setup", TestContext.Current.CancellationToken);
        return coordinator;
    }

    private static SubmitCommand Command()
        => new(
            "01HXCNTRCT0000000000000000",
            "tenant-contract-a",
            "orders",
            "order-1",
            "ShipOrder",
            [],
            "01HXCNTRCT0000000000000000",
            "user-contract-a");

    private static SubmitQuery Query()
        => new(
            "tenant-contract-a",
            "orders",
            "order-1",
            "GetOrders",
            [],
            "01HXCNTRCT0000000000000000",
            "user-contract-a");
}
