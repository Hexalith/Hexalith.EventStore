using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class DaprSnapshotPolicyRepositoryTests {
    [Fact]
    public async Task SetPolicyAsync_WritesBothIndexesAndPreservesCreatedAtOnUpdate() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset createdAt = DateTimeOffset.Parse("2026-05-21T10:00:00Z");
        var existing = new List<SnapshotPolicy> {
            new("tenant-a", "orders", "OrderAggregate", 50, createdAt),
        };
        SetupRead(daprClient, existing);
        SetupEtag(daprClient, existing, true);
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        var result = await repository.SetPolicyAsync(new SnapshotPolicySetRequest("tenant-a", "orders", "orderaggregate", 100));

        result.Success.ShouldBeTrue();
        _ = await daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            Arg.Is<string>(key => key == "admin:storage-snapshot-policies:all" || key == "admin:storage-snapshot-policies:tenant-a"),
            Arg.Is<List<SnapshotPolicy>>(policies =>
                policies.Count == 1
                && policies[0].IntervalEvents == 100
                && policies[0].CreatedAtUtc == createdAt),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(9, "RejectedValidation")]
    [InlineData(10, null)]
    [InlineData(100000, null)]
    [InlineData(100001, "RejectedValidation")]
    public async Task SetPolicyAsync_EnforcesIntervalBoundaries(int intervalEvents, string? expectedErrorCode) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupRead(daprClient, []);
        SetupEtag(daprClient, [], true);
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        var result = await repository.SetPolicyAsync(new SnapshotPolicySetRequest("tenant-a", "orders", "OrderAggregate", intervalEvents));

        result.ErrorCode.ShouldBe(expectedErrorCode);
    }

    [Fact]
    public async Task DeletePolicyAsync_MissingPolicyDoesNotMutateIndexes() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupRead(daprClient, []);
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        var result = await repository.DeletePolicyAsync(new SnapshotPolicyDeleteRequest("tenant-a", "orders", "OrderAggregate"));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NotFound");
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<List<SnapshotPolicy>>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default!,
            cancellationToken: default);
    }

    [Fact]
    public async Task GetIntervalAsync_UsesExactAggregateTypeOnly() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupRead(daprClient, [
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 25, DateTimeOffset.UtcNow),
        ]);
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        int? orderInterval = await repository.GetIntervalAsync("tenant-a", "orders", "OrderAggregate");
        int? invoiceInterval = await repository.GetIntervalAsync("tenant-a", "orders", "InvoiceAggregate");

        orderInterval.ShouldBe(25);
        invoiceInterval.ShouldBeNull();
    }

    [Fact]
    public async Task SetPolicyAsync_RepairsTenantScopeFromGlobalScopeAndPreservesCreatedAt() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DateTimeOffset createdAt = DateTimeOffset.Parse("2026-05-21T10:00:00Z");
        var globalPolicies = new List<SnapshotPolicy> {
            new("tenant-a", "orders", "OrderAggregate", 50, createdAt),
        };
        var tenantPolicies = new List<SnapshotPolicy>();
        SetupReadByKey(daprClient, new Dictionary<string, List<SnapshotPolicy>> {
            ["admin:storage-snapshot-policies:all"] = globalPolicies,
            ["admin:storage-snapshot-policies:tenant-a"] = tenantPolicies,
        });
        SetupEtagByKey(daprClient, new Dictionary<string, List<SnapshotPolicy>> {
            ["admin:storage-snapshot-policies:all"] = globalPolicies,
            ["admin:storage-snapshot-policies:tenant-a"] = tenantPolicies,
        });
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        var result = await repository.SetPolicyAsync(new SnapshotPolicySetRequest("tenant-a", "orders", "OrderAggregate", 75));

        result.Success.ShouldBeTrue();
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            "admin:storage-snapshot-policies:tenant-a",
            Arg.Is<List<SnapshotPolicy>>(policies =>
                policies.Count == 1
                && policies[0].IntervalEvents == 75
                && policies[0].CreatedAtUtc == createdAt),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePolicyAsync_RepairsStaleTenantOnlyPolicyInBothScopes() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var tenantPolicies = new List<SnapshotPolicy> {
            new("tenant-a", "orders", "OrderAggregate", 50, DateTimeOffset.UtcNow),
            new("tenant-a", "orders", "InvoiceAggregate", 25, DateTimeOffset.UtcNow),
        };
        SetupReadByKey(daprClient, new Dictionary<string, List<SnapshotPolicy>> {
            ["admin:storage-snapshot-policies:all"] = [],
            ["admin:storage-snapshot-policies:tenant-a"] = tenantPolicies,
        });
        SetupEtagByKey(daprClient, new Dictionary<string, List<SnapshotPolicy>> {
            ["admin:storage-snapshot-policies:all"] = [],
            ["admin:storage-snapshot-policies:tenant-a"] = tenantPolicies,
        });
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        var result = await repository.DeletePolicyAsync(new SnapshotPolicyDeleteRequest("tenant-a", "orders", "OrderAggregate"));

        result.Success.ShouldBeTrue();
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            "admin:storage-snapshot-policies:all",
            Arg.Is<List<SnapshotPolicy>>(policies =>
                policies.Count == 1
                && policies[0].AggregateType == "InvoiceAggregate"),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPolicyAsync_MatchesRuntimeFullTypeNameToSimplePolicyName() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupRead(daprClient, [
            new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 25, DateTimeOffset.UtcNow),
        ]);
        DaprSnapshotPolicyRepository repository = CreateRepository(daprClient);

        int? interval = await repository.GetIntervalAsync("tenant-a", "orders", "Samples.Orders.OrderAggregate");

        interval.ShouldBe(25);
    }

    private static DaprSnapshotPolicyRepository CreateRepository(DaprClient daprClient)
        => new(
            daprClient,
            Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
            NullLogger<DaprSnapshotPolicyRepository>.Instance);

    private static void SetupRead(DaprClient daprClient, List<SnapshotPolicy> policies)
        => _ = daprClient.GetStateAsync<List<SnapshotPolicy>>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => policies);

    private static void SetupReadByKey(DaprClient daprClient, IReadOnlyDictionary<string, List<SnapshotPolicy>> policiesByKey)
        => _ = daprClient.GetStateAsync<List<SnapshotPolicy>>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(callInfo => policiesByKey.TryGetValue(callInfo.ArgAt<string>(1), out List<SnapshotPolicy>? policies)
                ? policies
                : []);

    private static void SetupEtag(DaprClient daprClient, List<SnapshotPolicy> policies, bool saveResult) {
        _ = daprClient.GetStateAndETagAsync<List<SnapshotPolicy>>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (policies, "etag"));

        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<SnapshotPolicy>>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(saveResult);
    }

    private static void SetupEtagByKey(DaprClient daprClient, IReadOnlyDictionary<string, List<SnapshotPolicy>> policiesByKey) {
        _ = daprClient.GetStateAndETagAsync<List<SnapshotPolicy>>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(callInfo => (
                policiesByKey.TryGetValue(callInfo.ArgAt<string>(1), out List<SnapshotPolicy>? policies) ? policies : [],
                "etag"));

        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<SnapshotPolicy>>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
    }
}
