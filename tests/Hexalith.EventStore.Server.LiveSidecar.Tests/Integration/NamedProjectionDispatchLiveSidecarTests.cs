using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
    private const int RetryLedgerShardCount = 64;
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
        detailCheckpoint.RootElement.GetProperty("writerProtocolVersion").GetInt32().ShouldBe(2);
        detailCheckpoint.RootElement.GetProperty("activeReservation").ValueKind.ShouldBe(JsonValueKind.Null);
        JsonDocument activeIndexDelivery = JsonDocument.Parse(
            (await ReadStateJsonAsync(database, indexCheckpointKey).ConfigureAwait(true)).ShouldNotBeNull());
        activeIndexDelivery.RootElement.GetProperty("lastDeliveredSequence").GetInt64().ShouldBe(0);
        activeIndexDelivery.RootElement.GetProperty("activeReservation").ValueKind.ShouldBe(JsonValueKind.Object);
        activeIndexDelivery.RootElement.GetProperty("activeReservation").GetProperty("dispatchId").GetString()
            .ShouldBe(head.MessageId);

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
        foreach (string projectionType in new[] { "counter-detail", "counter-index" }) {
            string lifecycleActorId = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:{projectionType}";
            string lifecycleKey = $"{AppId}||{ProjectionLifecycleActor.ActorTypeName}||{lifecycleActorId}||projection-lifecycle";
            (await database.HashGetAsync(lifecycleKey, "data").ConfigureAwait(true)).IsNullOrEmpty.ShouldBeTrue(
                "normal delivery must preserve the persisted lifecycle baseline (absent means idle)");
        }

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
        ProjectionDeliveryRetryWorkItem unrelatedTerminalWork = await recreatedScheduler.ScheduleAsync(
            CreateUnrelatedSameShardWorkItem(persistedWork),
            CancellationToken.None).ConfigureAwait(true);
        retryJson = (await database.HashGetAsync(retryStateKey, "data").ConfigureAwait(true)).ToString();
        retryJson.ShouldContain(persistedWork.WorkId);
        retryJson.ShouldContain(unrelatedTerminalWork.WorkId);

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
        indexCheckpoint.RootElement.GetProperty("activeReservation").ValueKind.ShouldBe(JsonValueKind.Null);

        string convergedRetryJson = (await database.HashGetAsync(retryStateKey, "data").ConfigureAwait(true)).ToString();
        using JsonDocument convergedRetryLedger = JsonDocument.Parse(convergedRetryJson);
        string[] convergedWorkIds = [.. convergedRetryLedger.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(static item => item.GetProperty("workId").GetString())
            .OfType<string>()];
        convergedWorkIds.ShouldNotContain(
            persistedWork.WorkId,
            "retry convergence removes this aggregate's work without requiring its whole shard to be empty");
        convergedWorkIds.ShouldContain(
            unrelatedTerminalWork.WorkId,
            "convergence must preserve unrelated terminal work that shares the retry-ledger shard");
        ProjectionDeliveryRetryWorkItem claimedUnrelatedWork = (await recreatedScheduler.TryAcquireAsync(
            unrelatedTerminalWork,
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1),
            CancellationToken.None).ConfigureAwait(true)).ShouldNotBeNull();
        (await recreatedScheduler.TryDeleteAsync(claimedUnrelatedWork, CancellationToken.None).ConfigureAwait(true))
            .ShouldBeTrue();

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

        faultControl.DetailInvocationCount.ShouldBe(1);
        faultControl.IndexInvocationCount.ShouldBe(2);
        string detailBeforeDuplicate = (await database.HashGetAsync($"{AppId}||{detailKey}", "data").ConfigureAwait(true)).ToString();
        string indexBeforeDuplicate = (await database.HashGetAsync($"{AppId}||{indexKey}", "data").ConfigureAwait(true)).ToString();
        await orchestrator.DeliverProjectionAsync(identity).ConfigureAwait(true);
        faultControl.DetailInvocationCount.ShouldBe(1, "completed duplicates must not invoke /project/v2 handlers");
        faultControl.IndexInvocationCount.ShouldBe(2, "completed duplicates must not invoke /project/v2 handlers");
        (await database.HashGetAsync($"{AppId}||{detailKey}", "data").ConfigureAwait(true)).ToString()
            .ShouldBe(detailBeforeDuplicate);
        (await database.HashGetAsync($"{AppId}||{indexKey}", "data").ConfigureAwait(true)).ToString()
            .ShouldBe(indexBeforeDuplicate);

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

    [Fact]
    [Trait("Tier", "3")]
    public async Task ConcurrentDuplicateReverseAndConflict_StayEquivalentToOneInOrderDelivery() {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        IServiceProvider services = fixture.Services;
        string aggregateId = $"named-concurrent-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        EventEnvelope[] events = await CreateAggregateHistoryAsync(identity, 2).ConfigureAwait(true);
        ProjectionEventDto[] projectionEvents = (await ProjectionEventWireBuilder.BuildAsync(
            services.GetRequiredService<IEventPayloadProtectionService>(),
            identity,
            events,
            CancellationToken.None).ConfigureAwait(true)).Events.ShouldNotBeNull();
        AdminOperationalIndexHostedService catalogLoader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        DomainServiceRegistration registration = Registration(identity);
        INamedProjectionDispatchCoordinator first = services.GetRequiredService<INamedProjectionDispatchCoordinator>();
        INamedProjectionDispatchCoordinator second = services.GetRequiredService<INamedProjectionDispatchCoordinator>();
        LiveNamedProjectionFaultControl evidence = services.GetRequiredService<LiveNamedProjectionFaultControl>();

        await Task.WhenAll(
            first.TryDispatchAsync(identity, registration, events, projectionEvents, CancellationToken.None),
            second.TryDispatchAsync(identity, registration, events, projectionEvents, CancellationToken.None)).ConfigureAwait(true);

        evidence.DetailInvocationCount.ShouldBe(1);
        evidence.IndexInvocationCount.ShouldBe(1);
        using DaprClient client = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.DaprHttpEndpoint)
            .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
            .Build();
        string detailDeliveryKey = ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-detail");
        string indexDeliveryKey = ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-index");
        ProjectionDeliveryState detailBaseline = (await client.GetStateAsync<ProjectionDeliveryState>(StoreName, detailDeliveryKey))
            .ShouldNotBeNull();
        ProjectionDeliveryState indexBaseline = (await client.GetStateAsync<ProjectionDeliveryState>(StoreName, indexDeliveryKey))
            .ShouldNotBeNull();
        string detailBaselineJson = JsonSerializer.Serialize(detailBaseline);
        string indexBaselineJson = JsonSerializer.Serialize(indexBaseline);
        detailBaseline.LastDeliveredSequence.ShouldBe(2);
        indexBaseline.LastDeliveredSequence.ShouldBe(2);
        await using ConnectionMultiplexer redis = await ConnectionMultiplexer
            .ConnectAsync("localhost:6379,abortConnect=false,allowAdmin=true")
            .ConfigureAwait(true);
        IDatabase database = redis.GetDatabase();
        string detailModelKey = $"{AppId}||{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:detail";
        string indexModelKey = $"{AppId}||{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:index";
        string detailModelBaseline = (await database.HashGetAsync(detailModelKey, "data").ConfigureAwait(true)).ToString();
        string indexModelBaseline = (await database.HashGetAsync(indexModelKey, "data").ConfigureAwait(true)).ToString();
        var detailBatchScope = new ReadModelBatchScope(
            StoreName,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            "counter-detail",
            events[^1].MessageId);
        var indexBatchScope = new ReadModelBatchScope(
            StoreName,
            identity.TenantId,
            identity.Domain,
            identity.AggregateId,
            "counter-index",
            events[^1].MessageId);
        string detailReceiptBaseline = (await ResolveMarkerJsonAsync(database, detailBatchScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldNotBeNull();
        string indexReceiptBaseline = (await ResolveMarkerJsonAsync(database, indexBatchScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldNotBeNull();

        _ = await first.TryDispatchAsync(identity, registration, events, projectionEvents, CancellationToken.None).ConfigureAwait(true);
        evidence.DetailInvocationCount.ShouldBe(1);
        evidence.IndexInvocationCount.ShouldBe(1);

        ProjectionEventDto[] conflicting = [.. projectionEvents];
        conflicting[^1] = conflicting[^1] with { Payload = [99, 98, 97] };
        _ = await first.TryDispatchAsync(identity, registration, events, conflicting, CancellationToken.None).ConfigureAwait(true);
        evidence.DetailInvocationCount.ShouldBe(1);
        evidence.IndexInvocationCount.ShouldBe(1);

        _ = await first.TryDispatchAsync(
            identity,
            registration,
            events[..1],
            projectionEvents[..1],
            CancellationToken.None).ConfigureAwait(true);
        evidence.DetailInvocationCount.ShouldBe(1);
        evidence.IndexInvocationCount.ShouldBe(1);
        JsonSerializer.Serialize(await client.GetStateAsync<ProjectionDeliveryState>(StoreName, detailDeliveryKey))
            .ShouldBe(detailBaselineJson);
        JsonSerializer.Serialize(await client.GetStateAsync<ProjectionDeliveryState>(StoreName, indexDeliveryKey))
            .ShouldBe(indexBaselineJson);
        (await database.HashGetAsync(detailModelKey, "data").ConfigureAwait(true)).ToString()
            .ShouldBe(detailModelBaseline);
        (await database.HashGetAsync(indexModelKey, "data").ConfigureAwait(true)).ToString()
            .ShouldBe(indexModelBaseline);
        (await ResolveMarkerJsonAsync(database, detailBatchScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldBe(detailReceiptBaseline);
        (await ResolveMarkerJsonAsync(database, indexBatchScope.ComputeScopeHash()).ConfigureAwait(true))
            .ShouldBe(indexReceiptBaseline);
        await catalogLoader.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task GapThenCanonicalMissingEvent_DoesNotInvokeUntilContiguousAndThenConverges() {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        IServiceProvider services = fixture.Services;
        string aggregateId = $"named-gap-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        EventEnvelope[] events = await CreateAggregateHistoryAsync(identity, 3).ConfigureAwait(true);
        ProjectionEventDto[] projectionEvents = (await ProjectionEventWireBuilder.BuildAsync(
            services.GetRequiredService<IEventPayloadProtectionService>(),
            identity,
            events,
            CancellationToken.None).ConfigureAwait(true)).Events.ShouldNotBeNull();
        AdminOperationalIndexHostedService catalogLoader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        DomainServiceRegistration registration = Registration(identity);
        INamedProjectionDispatchCoordinator coordinator = services.GetRequiredService<INamedProjectionDispatchCoordinator>();
        LiveNamedProjectionFaultControl evidence = services.GetRequiredService<LiveNamedProjectionFaultControl>();

        _ = await coordinator.TryDispatchAsync(
            identity,
            registration,
            [events[0], events[2]],
            [projectionEvents[0], projectionEvents[2]],
            CancellationToken.None).ConfigureAwait(true);

        evidence.DetailInvocationCount.ShouldBe(0);
        evidence.IndexInvocationCount.ShouldBe(0);
        using DaprClient client = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.DaprHttpEndpoint)
            .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
            .Build();
        (await client.GetStateAsync<ProjectionDeliveryState>(
            StoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-detail"))).ShouldBeNull();

        _ = await coordinator.TryDispatchAsync(
            identity,
            registration,
            events,
            projectionEvents,
            CancellationToken.None).ConfigureAwait(true);

        evidence.DetailInvocationCount.ShouldBe(1);
        evidence.IndexInvocationCount.ShouldBe(1);
        ProjectionDeliveryState detail = (await client.GetStateAsync<ProjectionDeliveryState>(
            StoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-detail"))).ShouldNotBeNull();
        ProjectionDeliveryState index = (await client.GetStateAsync<ProjectionDeliveryState>(
            StoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-index"))).ShouldNotBeNull();
        detail.LastDeliveredSequence.ShouldBe(3);
        index.LastDeliveredSequence.ShouldBe(3);
        detail.ActiveReservation.ShouldBeNull();
        index.ActiveReservation.ShouldBeNull();
        await catalogLoader.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task ReconcileAbsentCheckpoint_PersistsHydratedRowAndOperatorEvidenceTogether() {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        var identity = new AggregateIdentity(
            "tenant-a",
            "counter",
            $"named-reconcile-{Guid.NewGuid():N}");
        const string projectionName = "counter-detail";

        ProjectionDeliveryReconciliationResult result = await services
            .GetRequiredService<IProjectionDeliveryReconciler>()
            .ReconcileFromEventStoreAsync(
                identity,
                projectionName,
                "live-operator")
            .ConfigureAwait(true);

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.Completed);
        result.PreservedSequence.ShouldBe(0);
        using DaprClient client = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.DaprHttpEndpoint)
            .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
            .Build();
        ProjectionDeliveryState state = (await client.GetStateAsync<ProjectionDeliveryState>(
            StoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, projectionName))).ShouldNotBeNull();
        ProjectionDeliveryReconciliationWork work = (await client.GetStateAsync<ProjectionDeliveryReconciliationWork>(
            StoreName,
            ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projectionName))).ShouldNotBeNull();
        state.LastDeliveredSequence.ShouldBe(0);
        state.MigrationProvenance.ShouldBe(ProjectionDeliveryMigrationProvenance.InitializedFromZero);
        work.OperatorId.ShouldBe("live-operator");
        work.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciled);
        work.ObservedSequence.ShouldBe(0);
        work.RecordedAt.ShouldBe(state.UpdatedAt);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task DowngradedDetailRow_FailsClosedWithoutHandlerWhileIndexSiblingCompletes() {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        IServiceProvider services = fixture.Services;
        string aggregateId = $"named-regression-{Guid.NewGuid():N}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        EventEnvelope[] events = await CreateAggregateHistoryAsync(identity, 1).ConfigureAwait(true);
        ProjectionEventDto[] projectionEvents = (await ProjectionEventWireBuilder.BuildAsync(
            services.GetRequiredService<IEventPayloadProtectionService>(),
            identity,
            events,
            CancellationToken.None).ConfigureAwait(true)).Events.ShouldNotBeNull();
        AdminOperationalIndexHostedService catalogLoader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        using DaprClient client = new DaprClientBuilder()
            .UseHttpEndpoint(fixture.DaprHttpEndpoint)
            .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
            .Build();
        string detailKey = ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-detail");
        await client.SaveStateAsync(
            StoreName,
            detailKey,
            new ProjectionCheckpoint(
                identity.TenantId,
                identity.Domain,
                identity.AggregateId,
                0,
                DateTimeOffset.UtcNow)).ConfigureAwait(true);
        INamedProjectionDispatchCoordinator coordinator = services.GetRequiredService<INamedProjectionDispatchCoordinator>();
        LiveNamedProjectionFaultControl evidence = services.GetRequiredService<LiveNamedProjectionFaultControl>();

        _ = await coordinator.TryDispatchAsync(
            identity,
            Registration(identity),
            events,
            projectionEvents,
            CancellationToken.None).ConfigureAwait(true);

        evidence.DetailInvocationCount.ShouldBe(0);
        evidence.IndexInvocationCount.ShouldBe(1);
        ProjectionDeliveryStateReadResult detail = await services.GetRequiredService<IProjectionDeliveryStateStore>()
            .ReadAsync(identity, "counter-detail")
            .ConfigureAwait(true);
        detail.Classification.ShouldBe(ProjectionDeliveryStateClassification.SchemaRegression);
        ProjectionDeliveryState index = (await client.GetStateAsync<ProjectionDeliveryState>(
            StoreName,
            ProjectionDeliveryStateKeys.GetStateKey(identity, "counter-index"))).ShouldNotBeNull();
        index.LastDeliveredSequence.ShouldBe(1);
        index.ActiveReservation.ShouldBeNull();
        await catalogLoader.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    [Fact]
    [Trait("Tier", "3")]
    public async Task PagedRebuild_MoreThanTwoPages_PersistsEquivalentRedisActorDetailIndexAndCheckpoints() {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        IServiceProvider services = fixture.Services;
        string aggregateId = $"named-rebuild-{UniqueIdHelper.GenerateSortableUniqueStringId()}";
        var identity = new AggregateIdentity("tenant-a", "counter", aggregateId);
        EventEnvelope[] events = await CreateAggregateHistoryAsync(identity, 7).ConfigureAwait(true);
        long expectedVersion = events.Max(static item => item.SequenceNumber);
        IProjectionCheckpointTracker tracker = services.GetRequiredService<IProjectionCheckpointTracker>();
        await tracker.TrackIdentityAsync(identity, CancellationToken.None).ConfigureAwait(true);
        AdminOperationalIndexHostedService catalogLoader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        var operatorScope = new ProjectionRebuildCheckpointScope(
            identity.TenantId,
            identity.Domain,
            "counter-legacy",
            AggregateId: identity.AggregateId,
            OperationId: UniqueIdHelper.GenerateSortableUniqueStringId());
        IProjectionRebuildCheckpointStore rebuildStore = services.GetRequiredService<IProjectionRebuildCheckpointStore>();
        ProjectionRebuildCheckpointSaveResult begin = await rebuildStore.ResetAsync(
            operatorScope,
            0,
            ProjectionRebuildStatus.Running,
            failureReasonCode: null,
            CancellationToken.None,
            toPosition: expectedVersion).ConfigureAwait(true);
        begin.Succeeded.ShouldBeTrue();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(identity.TenantId, identity.Domain, "v1", Arg.Any<CancellationToken>())
            .Returns(Registration(identity));
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions {
            HttpEndpoint = fixture.DaprHttpEndpoint,
        });
        ProjectionUpdateOrchestrator orchestrator = CreateOrchestrator(
            actorProxyFactory,
            resolver,
            new ProjectionOptions { RebuildPageSize = 3 });
        IProjectionDeliveryCheckpointStore delivery = services.GetRequiredService<IProjectionDeliveryCheckpointStore>();
        (await delivery.ReadDeliveredSequenceAsync(identity, "counter-detail", CancellationToken.None).ConfigureAwait(true))
            .ShouldBe(0);
        (await delivery.ReadDeliveredSequenceAsync(identity, "counter-index", CancellationToken.None).ConfigureAwait(true))
            .ShouldBe(0);

        await orchestrator.RebuildProjectionAsync(operatorScope with { OperationId = null }).ConfigureAwait(true);

        ProjectionRebuildCheckpoint operatorCheckpoint = (await rebuildStore
            .ReadAsync(operatorScope, CancellationToken.None)
            .ConfigureAwait(true)).ShouldNotBeNull();
        operatorCheckpoint.FailureReasonCode.ShouldBeNull();
        operatorCheckpoint.Status.ShouldBe(ProjectionRebuildStatus.Succeeded);
        operatorCheckpoint.LastAppliedSequence.ShouldBe(expectedVersion);

        await using ConnectionMultiplexer redis = await ConnectionMultiplexer
            .ConnectAsync("localhost:6379,abortConnect=false,allowAdmin=true")
            .ConfigureAwait(true);
        IDatabase database = redis.GetDatabase();
        string detailKey = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:detail";
        string indexKey = $"{identity.TenantId}:{identity.Domain}:{identity.AggregateId}:index";
        JsonDocument detail = JsonDocument.Parse(
            (await ReadStateJsonAsync(database, detailKey).ConfigureAwait(true)).ShouldNotBeNull());
        JsonDocument index = JsonDocument.Parse(
            (await ReadStateJsonAsync(database, indexKey).ConfigureAwait(true)).ShouldNotBeNull());
        detail.RootElement.GetProperty("eventCount").GetInt32().ShouldBe(events.Length);
        detail.RootElement.GetProperty("projectionVersion").GetString()
            .ShouldBe(expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        index.RootElement.GetProperty("aggregateId").GetString().ShouldBe(identity.AggregateId);
        index.RootElement.GetProperty("projectionVersion").GetString()
            .ShouldBe(expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));

        string legacyActorId = $"counter-legacy:{identity.TenantId}:{identity.AggregateId}";
        string legacyActorKey = $"{AppId}||{QueryRouter.ProjectionActorTypeName}||{legacyActorId}||{EventReplayProjectionActor.ProjectionStateKey}";
        JsonDocument legacy = JsonDocument.Parse(
            (await database.HashGetAsync(legacyActorKey, "data").ConfigureAwait(true)).ToString());
        legacy.RootElement.GetProperty("stateBytes").Deserialize<byte[]>()
            .ShouldNotBeNull()
            .ShouldNotBeEmpty();
        byte[] stateBytes = legacy.RootElement.GetProperty("stateBytes").Deserialize<byte[]>()!;
        JsonDocument actorState = JsonDocument.Parse(stateBytes);
        actorState.RootElement.GetProperty("eventCount").GetInt32().ShouldBe(events.Length);

        (await services.GetRequiredService<IProjectionLifecycleGateway>()
                .ReadPhaseAsync(identity, "counter-legacy", CancellationToken.None)
                .ConfigureAwait(true))
            .ShouldBe(ProjectionLifecyclePhase.Idle);
        (await delivery.ReadDeliveredSequenceAsync(identity, "counter-detail", CancellationToken.None).ConfigureAwait(true))
            .ShouldBe(0);
        (await delivery.ReadDeliveredSequenceAsync(identity, "counter-index", CancellationToken.None).ConfigureAwait(true))
            .ShouldBe(0);
        await catalogLoader.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private ProjectionUpdateOrchestrator CreateOrchestrator(
        IActorProxyFactory actorProxyFactory,
        IDomainServiceResolver resolver,
        ProjectionOptions? projectionOptions = null) {
        IServiceProvider services = fixture.Services;
        return new ProjectionUpdateOrchestrator(
            actorProxyFactory,
            services.GetRequiredService<DaprClient>(),
            services.GetRequiredService<IHttpClientFactory>(),
            resolver,
            services.GetRequiredService<IProjectionCheckpointTracker>(),
            projectionOptions is null
                ? services.GetRequiredService<IOptions<ProjectionOptions>>()
                : Options.Create(projectionOptions),
            NullLogger<ProjectionUpdateOrchestrator>.Instance,
            services.GetRequiredService<IProjectionRebuildCheckpointStore>(),
            services.GetRequiredService<IEventPayloadProtectionService>(),
            Options.Create(new EventStoreActorOptions { AggregateActorTypeName = fixture.AggregateActorTypeName }),
            services.GetRequiredService<IProjectionDeliveryCheckpointStore>(),
            services.GetRequiredService<IProjectionLifecycleGateway>(),
            services.GetRequiredService<INamedProjectionDispatchCoordinator>(),
            rebuildWriteGateway: services.GetRequiredService<IProjectionRebuildWriteGateway>());
    }

    private async Task<EventEnvelope[]> CreateAggregateHistoryAsync(AggregateIdentity identity, int eventCount) {
        var actorProxyFactory = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint });
        IAggregateActor aggregate = actorProxyFactory.CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId),
            fixture.AggregateActorTypeName);
        for (int index = 0; index < eventCount; index++) {
            CommandEnvelope command = new CommandEnvelopeBuilder()
                .WithTenantId(identity.TenantId)
                .WithDomain(identity.Domain)
                .WithAggregateId(identity.AggregateId)
                .WithCommandType("IncrementCounter")
                .Build();
            (await aggregate.ProcessCommandAsync(command).ConfigureAwait(true)).Accepted.ShouldBeTrue();
        }

        return await aggregate.GetEventsAsync(0).ConfigureAwait(true);
    }

    private async Task<AdminOperationalIndexHostedService> LoadCatalogAsync(AggregateIdentity identity) {
        IServiceProvider services = fixture.Services;
        var domainServiceOptions = new DomainServiceOptions();
        domainServiceOptions.Registrations[$"{identity.TenantId}|{identity.Domain}|v1"] = Registration(identity);
        var loader = new AdminOperationalIndexHostedService(
            services.GetRequiredService<DaprClient>(),
            services.GetRequiredService<IHttpClientFactory>(),
            Options.Create(new Hexalith.EventStore.Server.Commands.CommandStatusOptions()),
            Options.Create(domainServiceOptions),
            services.GetRequiredService<IOptions<ProjectionOptions>>(),
            services.GetRequiredService<INamedProjectionRouteCatalog>(),
            NullLogger<AdminOperationalIndexHostedService>.Instance,
            services.GetRequiredService<IOptions<ProjectionDispatchOptions>>());
        await loader.StartAsync(CancellationToken.None).ConfigureAwait(true);
        return loader;
    }

    private static DomainServiceRegistration Registration(AggregateIdentity identity) => new(
        AppId,
        "process",
        identity.TenantId,
        identity.Domain,
        "v1");

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

    private static ProjectionDeliveryRetryWorkItem CreateUnrelatedSameShardWorkItem(
        ProjectionDeliveryRetryWorkItem persistedWork) {
        int targetShard = SHA256.HashData(Encoding.UTF8.GetBytes(persistedWork.WorkId))[0] % RetryLedgerShardCount;
        string workId;
        do {
            workId = $"same-shard-terminal-{Guid.NewGuid():N}";
        }
        while (SHA256.HashData(Encoding.UTF8.GetBytes(workId))[0] % RetryLedgerShardCount != targetShard);

        string messageId = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        return persistedWork with {
            WorkId = workId,
            AggregateId = $"unrelated-{Guid.NewGuid():N}",
            HeadMessageId = messageId,
            PendingRoutes = [],
            TerminalRoutes = ["counter-detail"],
            DispatchId = messageId,
            Attempt = 1,
            NextDueUtc = DateTimeOffset.UtcNow,
            LastReasonCode = ProjectionDispatchReasonCodes.HandlerFailure,
            Revision = 0,
            LeaseOwner = null,
            LeaseExpiresUtc = null,
            ReservationFencingTokens = new Dictionary<string, long>(StringComparer.Ordinal),
        };
    }
}
