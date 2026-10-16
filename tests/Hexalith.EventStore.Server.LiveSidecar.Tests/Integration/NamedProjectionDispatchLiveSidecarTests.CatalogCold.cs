using System.Text.Json;

using Dapr.Actors;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class NamedProjectionDispatchLiveSidecarTests
{
    [Fact]
    public async Task SourceGuard_CatalogColdControlWorkIsMeasuredSeparatelyFromWarmCurrent()
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        IServiceProvider services = fixture.Services;
        IAggregateActor source = fixture.CreateObservedActorProxyFactory().CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, 1).ConfigureAwait(true);
        IReadOnlyDictionary<string, long> before = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            IReadOnlyDictionary<string, long> after = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            IReadOnlyDictionary<string, long> cold = DaprTestContainerFixture.NativeAttemptDelta(before, after);
            cold.GetValueOrDefault("primary/app_callbacks/metadata").ShouldBe(1);
            cold.GetValueOrDefault("primary/sdk_http/service_invocation").ShouldBe(1);
            cold.GetValueOrDefault("primary/app_callbacks/actor_methods").ShouldBe(0);
            cold.GetValueOrDefault("primary/app_callbacks/projections").ShouldBe(0);
            cold["primary/app_callbacks/metadata_request_bytes"].ShouldBeGreaterThan(0);
            cold["primary/app_callbacks/metadata_response_bytes"].ShouldBeInRange(1, 16_384);
            long saves = cold.Where(static item => item.Key.Contains("/native/", StringComparison.Ordinal)
                && item.Key.EndsWith("/dapr.proto.runtime.v1.Dapr/SaveState", StringComparison.Ordinal)).Sum(static item => item.Value);
            saves.ShouldBeInRange(1, 16);
            NamedProjectionRouteCatalogEntry catalog = services.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            catalog.SourceGuardCapabilities.ShouldContainKey(LiveGuardedCounterProjectionHandler.Route);
            DaprClient dapr = services.GetRequiredService<DaprClient>();
            var persistedBytes = new List<int>();
            foreach (string key in new[] { "admin:projections:all", "admin:projections:tenant-a", "admin:type-catalog:events:all",
                "admin:type-catalog:commands:all", "admin:type-catalog:aggregates:all" })
            {
                (ReadOnlyMemory<byte> body, _) = await dapr.GetByteStateAndETagAsync(StoreName, key).ConfigureAwait(true);
                body.Length.ShouldBeInRange(1, 16_384);
                using JsonDocument json = JsonDocument.Parse(body);
                json.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
                persistedBytes.Add(body.Length);
            }
            await SeedGuardedBaselineAsync(identity, source, catalog, 1).ConfigureAwait(true);
            SourceProof baseline = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            (await services.GetRequiredService<ProjectionSourceGuardQualification>().QualifyAsync(identity,
                LiveGuardedCounterProjectionHandler.Route, 1, baseline.SourceIncarnation, GuardFixtureMaintenance(), true).ConfigureAwait(true))
                .Qualified.ShouldBeTrue();
            before = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            using var warm = new ProjectionDeliveryCostListener();
            (await ((ProjectionSourceGuardDelivery)services.GetRequiredService<IProjectionSourceGuardDelivery>())
                .TryDeliverAsync(identity, 256, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            warm.Dispose();
            after = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            IReadOnlyDictionary<string, long> warmNative = DaprTestContainerFixture.NativeAttemptDelta(before, after);
            warmNative.GetValueOrDefault("primary/app_callbacks/metadata").ShouldBe(0);
            warmNative.GetValueOrDefault("primary/app_callbacks/metadata_request_bytes").ShouldBe(0);
            warmNative.GetValueOrDefault("primary/app_callbacks/metadata_response_bytes").ShouldBe(0);
            warm[ProjectionDeliveryCost.SourceInspections].ShouldBe(2);
            warm[ProjectionDeliveryCost.ProofReads].ShouldBe(1);
            warm[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
            warm[ProjectionDeliveryCost.MetadataReads].ShouldBe(6);
            warm[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
            warm[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
            warm[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            warm[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardCatalogColdWarm " + JsonSerializer.Serialize(new
            { cold, coldStateSaveRpcs = saves, owningIndexRowBytes = persistedBytes, warm = warmNative,
                warmCost = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static value => value.ToString(), value => warm[value]) }));
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }
}
