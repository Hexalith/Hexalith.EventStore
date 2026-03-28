#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprActorQueryServiceTests
{
    private const string StateStoreName = "statestore";
    private const string EventStoreAppId = "eventstore";

    private static DaprInfrastructureQueryService CreateService(
        DaprClient? daprClient = null,
        AdminServerOptions? serverOptions = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        daprClient ??= Substitute.For<DaprClient>();
        serverOptions ??= new AdminServerOptions
        {
            StateStoreName = StateStoreName,
            EventStoreAppId = EventStoreAppId,
        };
        httpClientFactory ??= Substitute.For<IHttpClientFactory>();
        IOptions<AdminServerOptions> options = Options.Create(serverOptions);

        return new DaprInfrastructureQueryService(
            daprClient,
            httpClientFactory,
            options,
            NullLogger<DaprInfrastructureQueryService>.Instance);
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsActorTypes_WhenLocalMetadataHasActors()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "test-app",
            actors: [new DaprActorMetadata("AggregateActor", 10), new DaprActorMetadata("ETagActor", 5)],
            extended: new Dictionary<string, string>(),
            components: []);

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorRuntimeInfo result = await service.GetActorRuntimeInfoAsync();

        result.ActorTypes.Count.ShouldBe(2);
        result.TotalActiveActors.ShouldBe(15);
        result.IsRemoteMetadataAvailable.ShouldBeTrue();
        result.ActorTypes[0].TypeName.ShouldBe("AggregateActor");
        result.ActorTypes[0].Description.ShouldContain("commands");
        result.ActorTypes[1].TypeName.ShouldBe("ETagActor");
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsDefaultConfig()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "test-app",
            actors: [new DaprActorMetadata("ETagActor", 1)],
            extended: new Dictionary<string, string>(),
            components: []);

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorRuntimeInfo result = await service.GetActorRuntimeInfoAsync();

        result.Configuration.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(60));
        result.Configuration.ScanInterval.ShouldBe(TimeSpan.FromSeconds(30));
        result.Configuration.DrainOngoingCallTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        result.Configuration.DrainRebalancedActors.ShouldBeTrue();
        result.Configuration.ReentrancyEnabled.ShouldBeFalse();
        result.Configuration.ReentrancyMaxStackDepth.ShouldBe(32);
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ReturnsEmpty_WhenSidecarUnavailable()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Sidecar down"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorRuntimeInfo result = await service.GetActorRuntimeInfoAsync();

        result.ActorTypes.ShouldBeEmpty();
        result.TotalActiveActors.ShouldBe(0);
        result.IsRemoteMetadataAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_ExcludesUnknownCountFromTotal()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprMetadata metadata = new(
            id: "test-app",
            actors: [new DaprActorMetadata("AggregateActor", 10), new DaprActorMetadata("ETagActor", -1)],
            extended: new Dictionary<string, string>(),
            components: []);

        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorRuntimeInfo result = await service.GetActorRuntimeInfoAsync();

        result.TotalActiveActors.ShouldBe(10);
    }

    [Fact]
    public async Task GetActorRuntimeInfoAsync_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.GetMetadataAsync(Arg.Any<CancellationToken>())
            .Returns<DaprMetadata>(_ => throw new OperationCanceledException());

        DaprInfrastructureQueryService service = CreateService(daprClient);

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.GetActorRuntimeInfoAsync(cts.Token));
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ReturnsNull_WhenActorTypeUnknown()
    {
        DaprInfrastructureQueryService service = CreateService();

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("UnknownActor", "some-id");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ReadsStateKeys_ForETagActor()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        string composedKey = $"{EventStoreAppId}||ETagActor||Proj:Tenant1||etag";

        daprClient.GetStateAsync<string>(
            StateStoreName,
            composedKey,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => "{\"value\":\"abc123\"}");

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("ETagActor", "Proj:Tenant1");

        result.ShouldNotBeNull();
        result.ActorType.ShouldBe("ETagActor");
        result.ActorId.ShouldBe("Proj:Tenant1");
        result.StateEntries.Count.ShouldBe(1);
        result.StateEntries[0].Key.ShouldBe("etag");
        result.StateEntries[0].Found.ShouldBeTrue();
        result.StateEntries[0].JsonValue.ShouldBe("{\"value\":\"abc123\"}");
        result.StateEntries[0].EstimatedSizeBytes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_HandlesNotFoundStateKeys()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();

        // Returns null for all state reads (not found)
        daprClient.GetStateAsync<string>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (string?)null);

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("ETagActor", "Proj:Tenant1");

        result.ShouldNotBeNull();
        result.StateEntries.ShouldAllBe(e => !e.Found);
        result.TotalSizeBytes.ShouldBe(0);
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ComputesTotalSize()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        string composedKey = $"{EventStoreAppId}||ETagActor||Proj:T1||etag";

        daprClient.GetStateAsync<string>(
            StateStoreName,
            composedKey,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => "test");

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("ETagActor", "Proj:T1");

        result.ShouldNotBeNull();
        result.TotalSizeBytes.ShouldBe(4); // "test" = 4 UTF-8 bytes
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_HandlesExceptionOnStateRead()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();

        daprClient.GetStateAsync<string>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("State store error"));

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("ETagActor", "Proj:T1");

        result.ShouldNotBeNull();
        result.StateEntries[0].Found.ShouldBeFalse();
        result.StateEntries[0].JsonValue.ShouldBeNull();
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ActorIdWithColons_RoundTrips()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        string actorId = "tenant1:mydomain:aggregate-123";
        string composedKey = $"{EventStoreAppId}||AggregateActor||{actorId}||pending_command_count";

        daprClient.GetStateAsync<string>(
            StateStoreName,
            composedKey,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => "5");

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("AggregateActor", actorId);

        result.ShouldNotBeNull();
        result.ActorId.ShouldBe(actorId);

        // Verify the pending_command_count key was read
        DaprActorStateEntry? pendingEntry = result.StateEntries
            .FirstOrDefault(e => e.Key == "pending_command_count");
        pendingEntry.ShouldNotBeNull();
        pendingEntry!.Found.ShouldBeTrue();
        pendingEntry.JsonValue.ShouldBe("5");
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_ResolvesActorIdInStateKeys()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        string actorId = "t1:domain:agg1";

        // The metadata key should be resolved to "t1:domain:agg1:metadata"
        string metadataKey = $"{EventStoreAppId}||AggregateActor||{actorId}||{actorId}:metadata";
        daprClient.GetStateAsync<string>(
            StateStoreName,
            metadataKey,
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => "{\"seq\":42}");

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("AggregateActor", actorId);

        result.ShouldNotBeNull();

        DaprActorStateEntry? metadataEntry = result.StateEntries
            .FirstOrDefault(e => e.Key == "{actorId}:metadata");
        metadataEntry.ShouldNotBeNull();
        metadataEntry!.Found.ShouldBeTrue();
        metadataEntry.JsonValue.ShouldBe("{\"seq\":42}");
    }

    [Fact]
    public async Task GetActorInstanceStateAsync_DynamicFamilies_ReturnedAsNotFound()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();

        DaprInfrastructureQueryService service = CreateService(daprClient);

        DaprActorInstanceState? result = await service.GetActorInstanceStateAsync("AggregateActor", "t:d:a");

        result.ShouldNotBeNull();

        // Dynamic families should appear but with Found = false
        DaprActorStateEntry? dynamicEntry = result.StateEntries
            .FirstOrDefault(e => e.Key.Contains("{causationId}"));
        dynamicEntry.ShouldNotBeNull();
        dynamicEntry!.Found.ShouldBeFalse();
    }
}
