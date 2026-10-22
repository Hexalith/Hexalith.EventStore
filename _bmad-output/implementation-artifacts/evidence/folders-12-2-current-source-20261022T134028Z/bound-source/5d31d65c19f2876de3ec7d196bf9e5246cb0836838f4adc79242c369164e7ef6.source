using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Projections;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Theory]
    [InlineData('x')]
    [InlineData('~')]
    public async Task NearLimitPersistedOwnerProofKeepsTruthfulPhysicalBytesWithinProductionGatewayAdmission(char padding)
    {
        using DaprClient client = Client();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new DaprReadModelStore(client, Options.Create(new ReadModelBatchOptions { TimeProvider = clock }));
        var identity = new AggregateIdentity("owner-expiry-" + Guid.NewGuid().ToString("N"), "widget", "owner");
        var scope = new ReadModelBatchScope("statestore", identity.TenantId, identity.Domain, identity.AggregateId, "detail", "owner-proof");
        var owner = new ProjectionOwnerSelection(1, scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType,
            "owner", "generation", 1, scope.BatchId, scope.BatchId, "version-1");
        int overhead = ReadModelBatchCanonicalJson.Serialize(new { Count = 1, Padding = "", ProjectionOwner = owner, ProjectionVersion = owner.FreshnessVersion }).Length;
        string key = identity.ActorId + ":detail";
        ReadModelBatchOperation operation = ReadModelBatchOperation.WriteExpiring(key,
            new { Count = 1, Padding = new string(padding, 16_384 - overhead), ProjectionOwner = owner, ProjectionVersion = owner.FreshnessVersion },
            ReadModelBatchConcurrency.CreateOnly, clock.GetUtcNow().AddMinutes(1));
        operation.CanonicalValue.Length.ShouldBe(16_384);
        (await store.ExecuteAsync(new ReadModelBatch(scope, [operation]) { OwnerSelection = owner })).IsSuccess.ShouldBeTrue();
        ProjectionOwnerProofResult proof = await store.ReadOwnerProofAsync(new(scope.TenantId, scope.Domain, scope.AggregateId, scope.ProjectionType, "catalog"), new("statestore", "detail"));
        proof.Proof.ShouldNotBeNull();
        byte[] model = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(key), "data"))!;
        byte[] receipt = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.ComputeMarkerKey()), "data"))!;
        proof.ReadBodyBytes.ShouldBe(model.Length + receipt.Length);
        proof.ReadBodyBytes.ShouldBeLessThanOrEqualTo(32_768);
        using var http = new HttpClient(new FixedOwnerProofLiveHttpHandler(proof));
        IHttpClientFactory clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(Arg.Any<string>()).Returns(http);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        var snapshot = new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 7);
        lifecycle.ReadSnapshotAsync(identity, scope.ProjectionType, Arg.Any<CancellationToken>()).Returns(snapshot);
        var gateway = new DaprProjectionSelectedOwnerProofGateway(client, clients, lifecycle);
        var catalog = new NamedProjectionRouteCatalogEntry(fixture.AppId, "v1", scope.Domain, 2, "named", "catalog", [scope.ProjectionType]);
        ProjectionOwnerProofResult admitted = await gateway.ReadAsync(identity, catalog, scope.ProjectionType, snapshot, CancellationToken.None);
        admitted.Proof.ShouldNotBeNull().Selection.ShouldBe(owner);
        admitted.ReadBodyBytes.ShouldBe(proof.ReadBodyBytes);
        admitted.MetadataKeyReads.ShouldBe(4);
        TestContext.Current.TestOutputHelper!.WriteLine($"padding={padding}; logical-bytes=16384; physical-model-bytes={model.Length}; total-physical-proof-bytes={proof.ReadBodyBytes}; production-gateway-admitted=true; original-32768-guard-unchanged=true; HTTP-response-carrier=fixed-real-SDK-proof");
    }
}
