using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogProjectionMethodPlanTests
{
    [Theory]
    [InlineData("legacy", null)]
    [InlineData("named", null)]
    [InlineData("mixed", null)]
    [InlineData("other-tenant", null)]
    [InlineData("named-metadata-missing", null)]
    [InlineData("mixed-metadata-missing", null)]
    [InlineData("ambiguous-app", null)]
    [InlineData("legacy", "counter")]
    [InlineData("named", "counter-named")]
    [InlineData("mixed", "counter")]
    [InlineData("mixed", "counter-named")]
    [InlineData("legacy-guarded", null)]
    [InlineData("legacy-guarded", "counter")]
    [InlineData("legacy-unavailable", null)]
    [InlineData("legacy-unavailable", "counter")]
    public async Task NonemptyDeliveryExecutesOnlyTheTenantsCommittedMethodsAndPersistsTheirOwnOutputs(string mode, string? rebuildOutput)
    {
        ArgumentNullException.ThrowIfNull(mode);
        bool legacyOnly = mode.StartsWith("legacy", StringComparison.Ordinal);
        bool refusedScope = mode is "legacy-guarded" or "legacy-unavailable";
        var fixture = new RouteCatalogTestFixture();
        RouteContractDeclaration legacy = fixture.Envelope.Declarations.Single(d => d.RouteId == "counter-projection");
        var named = legacy with { RouteId = "counter-named-projection", ProjectionType = "counter-named", Execution = legacy.Execution! with
        { HandlerType = typeof(CatalogProjectionMethodPlanTests).FullName!, Aliases = ["counter-named"], ProjectionOutputs = ["counter-named"] } };
        RouteCatalogEntry legacyTarget = fixture.Envelope.Routes.Single(r => r.RouteId == legacy.RouteId);
        bool includeLegacy = legacyOnly || mode is "mixed" or "other-tenant" or "mixed-metadata-missing" or "ambiguous-app";
        bool includeNamed = !legacyOnly;
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = fixture.Envelope.Declarations.Where(d => d.RouteId != legacy.RouteId).Concat(includeLegacy ? [legacy] : []).Concat(includeNamed ? [named] : []).ToArray(),
            Routes = fixture.Envelope.Routes.Where(r => r.RouteId != legacy.RouteId)
                .Concat(includeLegacy ? [legacyTarget with { ResolverTenantPattern = mode == "other-tenant" ? "tenant-b" : "tenant-a" }] : [])
                .Concat(includeNamed ? [legacyTarget with { RouteId = named.RouteId, DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(named),
                    AppId = mode == "ambiguous-app" ? "foreign" : "sample", Method = "project/v2", ResolverTenantPattern = "tenant-a" }] : []).ToArray(),
        });
        string fingerprint = RouteCatalogTestFixture.Hash("method-plan-named-v1");
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            auxiliaryOperations: [.. new[] { "stage", "commit", "verify", "abort" }.Select(action => new RouteCatalogAuxiliaryOperation("sample", "project/rebuild/" + action + "/v1", EventStoreOperationIdentifiers.DomainServiceProject, "counter"))],
            projectionCapabilities: includeNamed ? [new(mode == "ambiguous-app" ? "foreign" : "sample", "counter", "v1", fingerprint, ["counter-named"])] : []);
        var authority = Substitute.For<IRouteCatalogAuthority>(); int leases = 0;
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => { leases++; return new RouteCatalogDispatchLease(snapshot, () => { leases--; return Task.CompletedTask; }); });
        var methods = new List<string>(); var states = new Dictionary<string, byte[]>(); var checkpoints = new Dictionary<string, long>();
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0"); await using var sink = builder.Build();
        sink.MapPost("/v1.0/invoke/{app}/method/project", async (HttpContext context) =>
        {
            methods.Add("project"); (await context.Request.ReadFromJsonAsync<ProjectionRequest>())!.Events.ShouldHaveSingleItem();
            return Results.Json(new ProjectionResponse("counter", JsonSerializer.SerializeToElement(new { count = 17 })));
        });
        sink.MapPost("/v1.0/invoke/{app}/method/project/v2", async (HttpContext context) =>
        {
            methods.Add("project/v2"); var request = (await context.Request.ReadFromJsonAsync<ProjectionDispatchRequest>())!;
            request.Request.Events.ShouldHaveSingleItem(); request.ProjectionTypes.ShouldBe(["counter-named"]);
            return Results.Json(new ProjectionDispatchResponse(2, [new("counter-named", ProjectionDispatchStatus.Completed, JsonSerializer.SerializeToElement(new { count = 18 }), null)]));
        });
        sink.MapPost("/v1.0/invoke/{app}/method/project/rebuild/{action}/v1", async (HttpContext context, string action) =>
        {
            methods.Add("project/rebuild/" + action + "/v1");
            var request = (await context.Request.ReadFromJsonAsync<ProjectionDispatchRequest>())!; request.Request.Events.ShouldHaveSingleItem();
            request.ProjectionTypes.ShouldBe(["counter-named"]);
            if (action == "commit") states["counter-named"] = JsonSerializer.SerializeToUtf8Bytes(new { count = 18 });
            return Results.Json(new ProjectionDispatchResponse(2, [new("counter-named", ProjectionDispatchStatus.Completed, null, null)]));
        });
        await sink.StartAsync(); using var dapr = new DaprClientBuilder().UseHttpEndpoint(sink.Urls.Single()).Build();
        using var client = new HttpClient(new RouteCatalogInvocationHandler(authority, new SocketsHttpHandler()));
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient(Arg.Any<string>()).Returns(client);
        var actors = Substitute.For<IActorProxyFactory>(); var source = Substitute.For<IAggregateActor>();
        source.GetEventsAsync(0).Returns([new EventEnvelope("01M61800000000000000000001", "aggregate", "counter", "tenant-a", "counter", 1, 1,
            DateTimeOffset.UtcNow, "correlation", "causation", "user", "v1", "CounterIncremented", 1, "json", "{\"amount\":17}"u8.ToArray(), null)]);
        source.GetCurrentSequenceAsync().Returns(1L);
        source.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<int>()).Returns(async call => call.ArgAt<long>(0) == 0 ? await source.GetEventsAsync(0) : []);
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(source);
        var write = Substitute.For<IProjectionWriteActor>(); write.UpdateProjectionAsync(Arg.Any<ProjectionState>()).Returns(c =>
        { var state = c.Arg<ProjectionState>(); states[state.ProjectionType] = state.StateBytes.ToArray(); return Task.CompletedTask; });
        actors.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(write);
        var etag = Substitute.For<IETagActor>(); etag.RegenerateAsync().Returns("etag");
        actors.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(etag);
        var delivery = Substitute.For<IProjectionDeliveryCheckpointStore>();
        delivery.SaveDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(c => { checkpoints[c.Arg<string>()] = c.Arg<long>(); return true; });
        var lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.BeginDeliveryWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.CompleteDeliveryWriteAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.BeginRebuildAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.CompleteRebuildAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.BeginRebuildPromotionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        lifecycle.CompleteRebuildPromotionAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var cache = new NamedProjectionRouteCatalog();
        if (!mode.EndsWith("metadata-missing", StringComparison.Ordinal)) cache.Replace(new([new("sample", "v1", "counter", 2, "fixture-capability", fingerprint, ["counter-named"])]));
        var coordinator = new NamedProjectionDispatchCoordinator(authority, cache, delivery, lifecycle, actors, dapr, clients,
            Options.Create(new ProjectionDispatchOptions()), NullLogger<NamedProjectionDispatchCoordinator>.Instance);
        var resolver = Substitute.For<IDomainServiceResolver>();
        var tracker = Substitute.For<IProjectionCheckpointTracker>(); tracker.EnumerateTrackedIdentitiesAsync(Arg.Any<CancellationToken>()).Returns(TrackedIdentity());
        var rebuildStore = Substitute.For<IProjectionRebuildCheckpointStore>();
        var row = new ProjectionRebuildCheckpoint("tenant-a", "counter", rebuildOutput ?? "counter", "aggregate", "operation", 0, ProjectionRebuildStatus.Running, DateTimeOffset.UtcNow, null, 1);
        rebuildStore.ReadAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<CancellationToken>()).Returns(_ => row);
        rebuildStore.SaveAsync(Arg.Any<ProjectionRebuildCheckpointScope>(), Arg.Any<long>(), Arg.Any<ProjectionRebuildStatus>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<long?>(), Arg.Any<bool>())
            .Returns(call =>
            {
                row = row with { LastAppliedSequence = call.ArgAt<long>(1), Status = call.ArgAt<ProjectionRebuildStatus>(2), FailureReasonCode = call.ArgAt<string?>(3), ToPosition = call.ArgAt<long?>(5) };
                checkpoints[row.ProjectionName] = row.LastAppliedSequence; return ProjectionRebuildCheckpointSaveResult.Success(row);
            });
        var sourceGuard = Substitute.For<IProjectionSourceGuardDelivery>();
        var guardStateStore = Substitute.For<IProjectionDeliveryStateStore>();
        var established = ProjectionDeliveryState.CreateEmpty(new("tenant-a", "counter", "aggregate"), "counter", RouteCatalogTestFixture.Hash("guarded-prefix"), DateTimeOffset.UtcNow)
            with { SchemaVersion = ProjectionDeliveryState.GuardSchemaVersion };
        if (mode == "legacy-unavailable") guardStateStore.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ProjectionDeliveryStateReadResult>(new IOException("state inspection unavailable")));
        else guardStateStore.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryStateReadResult(mode == "legacy-guarded" ? established : null, "etag", mode == "legacy-guarded"
                ? ProjectionDeliveryStateClassification.Current : ProjectionDeliveryStateClassification.Absent, mode == "legacy-guarded"));
        // No named metadata exists in this actual source-guard observer: its projection-specific
        // inspection must still recognize a persisted guarded row after catalog removal.
        var actualGuard = new ProjectionSourceGuardDelivery(new NamedProjectionRouteCatalog(), guardStateStore, lifecycle,
            Substitute.For<IProjectionSelectedOwnerProofGateway>(), actors, Options.Create(new EventStoreActorOptions()));
        sourceGuard.TryDeliverAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call => actualGuard.TryDeliverAsync(call.Arg<AggregateIdentity>(), call.Arg<CancellationToken>()));
        sourceGuard.InspectCurrentAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => actualGuard.InspectCurrentAsync(call.Arg<AggregateIdentity>(), call.Arg<string>(), call.Arg<CancellationToken>()));
        var orchestrator = new ProjectionUpdateOrchestrator(authority, actors, dapr, clients, resolver, tracker,
            Options.Create(new ProjectionOptions()), NullLogger<ProjectionUpdateOrchestrator>.Instance,
            deliveryCheckpointStore: delivery, namedProjectionDispatchCoordinator: coordinator,
            rebuildCheckpointStore: rebuildOutput is null ? null : rebuildStore, lifecycleGateway: lifecycle, sourceGuardDelivery: sourceGuard);
        if (refusedScope) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => rebuildOutput is null
            ? orchestrator.DeliverProjectionAsync(new("tenant-a", "counter", "aggregate"))
            : orchestrator.RebuildProjectionAsync(new("tenant-a", "counter", rebuildOutput, "aggregate", "operation")));
        else if (mode == "ambiguous-app") await Should.ThrowAsync<RouteCatalogUnavailableException>(() => orchestrator.DeliverProjectionAsync(new("tenant-a", "counter", "aggregate")));
        else if (rebuildOutput is not null) await orchestrator.RebuildProjectionAsync(new("tenant-a", "counter", rebuildOutput, "aggregate", "operation"));
        else await orchestrator.DeliverProjectionAsync(new("tenant-a", "counter", "aggregate"));
        string[] expected = refusedScope ? [] : rebuildOutput is null ? mode switch { "legacy" => ["project"], "mixed" => ["project/v2", "project"], "named" or "other-tenant" => ["project/v2"], _ => [] }
            : rebuildOutput == "counter" ? ["project"] : ["project/rebuild/stage/v1", "project/rebuild/commit/v1", "project/rebuild/verify/v1"];
        methods.ShouldBe(expected);
        string[] expectedOutputs = refusedScope ? [] : rebuildOutput is not null ? [rebuildOutput] : expected.Select(method => method == "project" ? "counter" : "counter-named").ToArray();
        states.Count.ShouldBe(expectedOutputs.Length); checkpoints.Count.ShouldBe(expectedOutputs.Length);
        foreach (string output in expectedOutputs)
        {
            checkpoints[output].ShouldBe(1); JsonSerializer.Deserialize<JsonElement>(states[output]).GetProperty("count").GetInt32().ShouldBe(output == "counter" ? 17 : 18);
        }
        if (refusedScope)
        {
            actors.ReceivedCalls().ShouldBeEmpty(); delivery.ReceivedCalls().ShouldBeEmpty(); rebuildStore.ReceivedCalls().ShouldBeEmpty();
            established.SchemaVersion.ShouldBe(ProjectionDeliveryState.GuardSchemaVersion);
        }
        else if (rebuildOutput is not null) row.Status.ShouldBe(ProjectionRebuildStatus.Succeeded);
        if (legacyOnly)
        {
            await sourceGuard.DidNotReceive().TryDeliverAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>());
            await sourceGuard.Received().InspectCurrentAsync(Arg.Any<AggregateIdentity>(), "counter", Arg.Any<CancellationToken>());
        }
        resolver.ReceivedCalls().ShouldBeEmpty(); leases.ShouldBe(0);
    }

    private static async IAsyncEnumerable<AggregateIdentity> TrackedIdentity()
    {
        yield return new("tenant-a", "counter", "aggregate");
        await Task.CompletedTask;
    }
}
