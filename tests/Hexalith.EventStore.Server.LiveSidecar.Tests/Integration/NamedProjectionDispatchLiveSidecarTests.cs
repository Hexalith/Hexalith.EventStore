using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Indexes;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Testing.Builders;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Tier-3 persisted-state evidence for normal v2 detail/index delivery and partial retry.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class NamedProjectionDispatchLiveSidecarTests(DaprTestContainerFixture fixture) {
    private const string AppId = "eventstore";
    private const string StoreName = "statestore";

    [Fact]
    [Trait("Tier", "3")]
    public async Task NormalDelivery_PersistsIndependentDetailIndexCheckpointsAndConvergedRetryLedger() {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        IServiceProvider services = fixture.Services;
        LiveNamedProjectionFaultControl faultControl = services.GetRequiredService<LiveNamedProjectionFaultControl>();
        faultControl.FailIndex = true;

        string aggregateId = $"named-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = fixture.DaprHttpEndpoint,
        });
        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            fixture.AggregateActorTypeName);
        CommandEnvelope command = new CommandEnvelopeBuilder()
            .WithTenantId(identity.TenantId)
            .WithDomain(identity.Domain)
            .WithAggregateId(identity.AggregateId)
            .WithCommandType("IncrementCounter")
            .Build();
        CommandProcessingResult commandResult = await aggregate.ProcessCommandAsync(command).ConfigureAwait(true);
        commandResult.Accepted.ShouldBeTrue();
        EventEnvelope head = (await aggregate.GetEventsAsync(0).ConfigureAwait(true)).ShouldHaveSingleItem();

        ProjectionDispatchRoute[] routes = [
            new("counter", "counter-detail"),
            new("counter", "counter-index"),
        ];
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(AppId, "v1", routes);
        var domainServiceOptions = new DomainServiceOptions();
        domainServiceOptions.Registrations["tenant-a|counter|v1"] = new DomainServiceRegistration(
            AppId,
            "process",
            identity.TenantId,
            identity.Domain,
            "v1");
        var catalogLoader = new AdminOperationalIndexHostedService(
            services.GetRequiredService<DaprClient>(),
            services.GetRequiredService<IHttpClientFactory>(),
            Options.Create(new Hexalith.EventStore.Server.Commands.CommandStatusOptions()),
            Options.Create(domainServiceOptions),
            services.GetRequiredService<IOptions<ProjectionOptions>>(),
            services.GetRequiredService<INamedProjectionRouteCatalog>(),
            NullLogger<AdminOperationalIndexHostedService>.Instance,
            services.GetRequiredService<IOptions<ProjectionDispatchOptions>>());
        await catalogLoader.StartAsync(CancellationToken.None).ConfigureAwait(true);

        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(identity.TenantId, identity.Domain, "v1", Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration(AppId, "process", identity.TenantId, identity.Domain, "v1"));
        ProjectionUpdateOrchestrator orchestrator = CreateOrchestrator(actorProxyFactory, resolver);

        await orchestrator.DeliverProjectionAsync(identity).ConfigureAwait(true);

        await using ConnectionMultiplexer redis = await ConnectionMultiplexer
            .ConnectAsync("localhost:6379,abortConnect=false,allowAdmin=true")
            .ConfigureAwait(true);
        IDatabase database = redis.GetDatabase();
        string detailKey = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:detail";
        string indexKey = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:index";
        JsonDocument detail = JsonDocument.Parse((await ReadStateJsonAsync(database, detailKey).ConfigureAwait(true)).ShouldNotBeNull());
        detail.RootElement.GetProperty("eventCount").GetInt32().ShouldBe(1);
        (await ReadStateJsonAsync(database, indexKey).ConfigureAwait(true)).ShouldBeNull();

        string detailCheckpointKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, "counter-detail");
        string indexCheckpointKey = ProjectionCheckpointTracker.GetProjectionScopedStateKey(identity, "counter-index");
        JsonDocument detailCheckpoint = JsonDocument.Parse(
            (await ReadStateJsonAsync(database, detailCheckpointKey).ConfigureAwait(true)).ShouldNotBeNull());
        detailCheckpoint.RootElement.GetProperty("lastDeliveredSequence").GetInt64().ShouldBe(head.SequenceNumber);
        (await ReadStateJsonAsync(database, indexCheckpointKey).ConfigureAwait(true)).ShouldBeNull();

        (await ReadStateJsonAsync(database, "projection-delivery-retry:ledger:v1").ConfigureAwait(true)).ShouldBeNull();
        (string retryStateKey, string retryJson) = (await ReadStateByPatternContainingAsync(
                database,
                "*projection-delivery-retry:ledger:v2:*",
                head.MessageId)
            .ConfigureAwait(true)).ShouldNotBeNull();
        retryJson.ShouldContain("counter-index");
        retryJson.ShouldContain(head.MessageId);
        retryJson.ShouldNotContain("payload", Case.Insensitive);

        string legacyActorId = $"counter-legacy:{identity.TenantId}:{identity.AggregateId}";
        string legacyActorKey = $"{AppId}||{QueryRouter.ProjectionActorTypeName}||{legacyActorId}||{EventReplayProjectionActor.ProjectionStateKey}";
        string legacyState = (await database.HashGetAsync(legacyActorKey, "data").ConfigureAwait(true)).ToString();
        legacyState.ShouldContain("counter-legacy");

        var recreatedScheduler = new DaprProjectionDeliveryRetryScheduler(
            services.GetRequiredService<DaprClient>(),
            services.GetRequiredService<IOptions<ProjectionOptions>>());
        ProjectionDeliveryRetryWorkItem persistedWork = (await recreatedScheduler
                .GetDueAsync(DateTimeOffset.UtcNow.AddMinutes(1), 8, CancellationToken.None)
                .ConfigureAwait(true))
            .Where(item => string.Equals(item.AggregateId, identity.AggregateId, StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        persistedWork.HeadSequence.ShouldBe(head.SequenceNumber);
        persistedWork.HeadMessageId.ShouldBe(head.MessageId);
        persistedWork.DispatchId.ShouldBe(head.MessageId);
        persistedWork.PendingRoutes.ShouldBe(["counter-index"]);

        faultControl.FailIndex = false;
        await Task.Delay(TimeSpan.FromMilliseconds(1100)).ConfigureAwait(true);
        ProjectionDeliveryRetryWorker hostedWorker = services.GetServices<IHostedService>()
            .OfType<ProjectionDeliveryRetryWorker>()
            .ShouldHaveSingleItem();
        await hostedWorker.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);

        JsonDocument index = JsonDocument.Parse((await ReadStateJsonAsync(database, indexKey).ConfigureAwait(true)).ShouldNotBeNull());
        index.RootElement.GetProperty("aggregateId").GetString().ShouldBe(identity.AggregateId);
        JsonDocument indexCheckpoint = JsonDocument.Parse(
            (await ReadStateJsonAsync(database, indexCheckpointKey).ConfigureAwait(true)).ShouldNotBeNull());
        indexCheckpoint.RootElement.GetProperty("lastDeliveredSequence").GetInt64().ShouldBe(head.SequenceNumber);

        string convergedRetryJson = (await database.HashGetAsync(retryStateKey, "data").ConfigureAwait(true)).ToString();
        convergedRetryJson.ShouldContain("\"items\":[]");

        var detailScope = new ReadModelBatchScope(
            StoreName,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            "counter-detail",
            head.MessageId);
        var indexScope = new ReadModelBatchScope(
            StoreName,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            "counter-index",
            head.MessageId);
        string detailReceipt = (await ResolveMarkerJsonAsync(database, detailScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldNotBeNull();
        string indexReceipt = (await ResolveMarkerJsonAsync(database, indexScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldNotBeNull();
        detailReceipt.ShouldContain("\"st\":4");
        indexReceipt.ShouldContain("\"st\":4");

        ProjectionEventReadabilityResult readability = await ProjectionEventWireBuilder
            .BuildAsync(
                services.GetRequiredService<IEventPayloadProtectionService>(),
                identity,
                [head],
                CancellationToken.None)
            .ConfigureAwait(true);
        using IServiceScope duplicateScope = services.CreateScope();
        ProjectionDispatchResponse duplicateResponse = await DomainProjectionDispatcher.DispatchAsync(
            duplicateScope.ServiceProvider,
            new ProjectionDispatchRequest(
                new ProjectionRequest(identity.TenantId, identity.Domain, identity.AggregateId, readability.Events!),
                routes.Select(static route => route.ProjectionType).ToArray(),
                head.MessageId,
                fingerprint),
            services.GetRequiredService<IOptions<ProjectionDispatchOptions>>().Value,
            services.GetRequiredService<IOptions<DomainProjectionIdentityOptions>>().Value,
            CancellationToken.None).ConfigureAwait(true);

        duplicateResponse.Outcomes.Select(static outcome => outcome.Status)
            .ShouldBe([ProjectionDispatchStatus.AlreadyCompleted, ProjectionDispatchStatus.AlreadyCompleted]);
        (await ResolveMarkerJsonAsync(database, detailScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldBe(detailReceipt);
        (await ResolveMarkerJsonAsync(database, indexScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldBe(indexReceipt);
        await catalogLoader.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private ProjectionUpdateOrchestrator CreateOrchestrator(
        IActorProxyFactory actorProxyFactory,
        IDomainServiceResolver resolver) {
        IServiceProvider services = fixture.Services;
        return new ProjectionUpdateOrchestrator(
            actorProxyFactory,
            services.GetRequiredService<DaprClient>(),
            services.GetRequiredService<IHttpClientFactory>(),
            resolver,
            services.GetRequiredService<IProjectionCheckpointTracker>(),
            services.GetRequiredService<IOptions<ProjectionOptions>>(),
            NullLogger<ProjectionUpdateOrchestrator>.Instance,
            services.GetRequiredService<IProjectionRebuildCheckpointStore>(),
            services.GetRequiredService<IEventPayloadProtectionService>(),
            Options.Create(new EventStoreActorOptions { AggregateActorTypeName = fixture.AggregateActorTypeName }),
            services.GetRequiredService<IProjectionDeliveryCheckpointStore>(),
            services.GetRequiredService<IProjectionLifecycleGateway>(),
            services.GetRequiredService<INamedProjectionDispatchCoordinator>());
    }

    private static async Task<byte[]?> ReadStateJsonAsync(IDatabase database, string logicalKey) {
        RedisValue value = await database.HashGetAsync($"{AppId}||{logicalKey}", "data").ConfigureAwait(true);
        if (!value.IsNullOrEmpty) {
            return (byte[]?)value;
        }

        var keys = (RedisResult[])(await database.ExecuteAsync("KEYS", $"*{logicalKey}").ConfigureAwait(true))!;
        foreach (RedisResult key in keys) {
            RedisValue candidate = await database.HashGetAsync((string)key!, "data").ConfigureAwait(true);
            if (!candidate.IsNullOrEmpty) {
                return (byte[]?)candidate;
            }
        }

        return null;
    }

    private static async Task<string?> ResolveMarkerJsonAsync(IDatabase database, string scopeHash) {
        var keys = (RedisResult[])(await database.ExecuteAsync("KEYS", $"*{scopeHash}*").ConfigureAwait(true))!;
        foreach (RedisResult key in keys) {
            RedisValue data = await database.HashGetAsync((string)key!, "data").ConfigureAwait(true);
            if (!data.IsNullOrEmpty) {
                return data.ToString();
            }
        }

        return null;
    }

    private static async Task<(string Key, string Json)?> ReadStateByPatternContainingAsync(
        IDatabase database,
        string pattern,
        string expectedText) {
        var keys = (RedisResult[])(await database.ExecuteAsync("KEYS", pattern).ConfigureAwait(true))!;
        foreach (RedisResult key in keys) {
            RedisValue data = await database.HashGetAsync((string)key!, "data").ConfigureAwait(true);
            if (!data.IsNullOrEmpty && data.ToString().Contains(expectedText, StringComparison.Ordinal)) {
                return ((string)key!, data.ToString());
            }
        }

        return null;
    }
}
