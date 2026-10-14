
using System.Text;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Snapshots;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorDomainResultTests {
    [Theory]
    [InlineData("bytes", false)]
    [InlineData("bytes", true)]
    [InlineData("graph", false)]
    [InlineData("graph", true)]
    public async Task ProcessCommandAsync_ActorEvidenceCeilingsAdmitTheBoundaryAndSkipOnlyExcess(
        string ceiling, bool atLimit)
    {
        // HX-FS-V1 §8.6, V16: UTF-8 and parsed graph limits are independent of the node ceiling.
        const string evidence = """{"value":"é"}""";
        int byteCount = Encoding.UTF8.GetByteCount(evidence);
        long graphBytes = 16L * byteCount + 64L * 4;
        FoldedSnapshotCaptureLimits limits = ceiling == "bytes"
            ? SnapshotTestLimits.ForState(byteCount - (atLimit ? 0 : 1))
            : SnapshotTestLimits.ForState(1024, actorGraphBytes: graphBytes - (atLimit ? 0 : 1));
        using ServiceProvider services = new ServiceCollection()
            .AddEventStoreFoldedSnapshotActorQualification("test-domain", "test-domain",
                "test-profile", "test-parity", limits)
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>()).Returns(true);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]) with { PostCommandStateJson = evidence });

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await ctx.StateManager.Received().SetStateAsync("test-tenant:test-domain:agg-001:events:4",
            Arg.Any<EventEnvelope>(), Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
        if (atLimit)
        {
            await ctx.SnapshotManager.Received(1).CreateSnapshotAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), 4,
                Arg.Is<object>(state => state is JsonElement && ((JsonElement)state).GetProperty("value").GetString() == "é"),
                ctx.StateManager, Arg.Any<JsonSerializerOptions>(), limits,
                Arg.Any<string?>(), Arg.Any<CancellationToken>(), false);
            snapshotLogger.Messages.ShouldNotContain(message => message.Contains(
                "FailureClass=SnapshotCaptureLimitExceeded", StringComparison.Ordinal));
        }
        else
        {
            await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<long>(),
                Arg.Any<object>(), Arg.Any<IActorStateManager>(), Arg.Any<JsonSerializerOptions>(),
                Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
            snapshotLogger.Messages.ShouldContain(message => message.Contains(
                "FailureClass=SnapshotCaptureLimitExceeded", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ProcessCommandAsync_UnadmittedSerializerBufferSkipsCaptureAndCommitsEvents()
    {
        // HX-FS-V1 §8.6, V6/V16: buffer refusal is advisory before even the envelope probe.
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            DefaultBufferSize = 64 * 1024 * 1024,
        };
        using ServiceProvider services = new ServiceCollection()
            .AddEventStoreFoldedSnapshotActorQualification("test-domain", "test-domain",
                "test-profile", "test-parity", SnapshotTestLimits.ForState())
            .Configure<ActorRuntimeOptions>(value => value.JsonSerializerOptions = options)
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await ctx.StateManager.Received().SetStateAsync("test-tenant:test-domain:agg-001:events:4",
            Arg.Any<EventEnvelope>(), Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<long>(),
            Arg.Any<object>(), Arg.Any<IActorStateManager>(), Arg.Any<JsonSerializerOptions>(),
            Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            "FailureClass=SnapshotCaptureLimitExceeded", StringComparison.Ordinal));
    }

    // === Story 3.4: State Rehydration Tests ===

    [Fact]
    public async Task ProcessCommandAsync_NewAggregate_RehydratesNullState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated") && o.ToString()!.Contains("null")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ExistingAggregate_RehydratesState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated") && o.ToString()!.Contains("DomainServiceCurrentState")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_RehydratedVersionedState_StopsBeforeLegacyInvoker() {
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
                "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UtcNow, null)));
        var stored = new EventEnvelope(
            "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "order-created", 2, "json", [1, 2, 3], null) {
            EventContractType = "order-created",
            PayloadVersion = 7,
        };
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>(
                "test-tenant:test-domain:agg-001:events:1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeFalse();
        _ = ctx.Invoker.DidNotReceive().InvokeAsync(
            Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_CorruptLogicalDigest_StopsBeforeDomainInvocation() {
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
                "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true,
                new AggregateMetadata(1, DateTimeOffset.UtcNow, null)));
        var stored = new EventEnvelope(
            "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", 1, 0,
            DateTimeOffset.UtcNow, "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", 1,
            "json", [1, 2, 3], null);
        stored = stored with {
            ApplicationPayloadDigest = EventLogicalDigest.Compute(stored, "json",
                EventLogicalDigest.HashPayload(stored.Payload)),
        };
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>(
                "test-tenant:test-domain:agg-001:events:1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored with { Payload = [4, 5, 6] }));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeFalse();
        _ = ctx.Invoker.DidNotReceive().InvokeAsync(
            Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ToContractEventEnvelope_PreservesVersionMetadataForVerifiedRoutes() {
        var stored = new EventEnvelope(
            "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "order-created", 2, "json", [1, 2, 3], null) {
            EventContractType = "order-created",
            PayloadVersion = 7,
        };

        var passedEvent = AggregateActor.ToContractEventEnvelope(stored);
        passedEvent.Metadata.MetadataVersion.ShouldBe(2);
        passedEvent.Metadata.EventContractType.ShouldBe("order-created");
        passedEvent.Metadata.PayloadVersion.ShouldBe(7);
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydrated_ProceedsToStep4() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Step 4 domain service invocation logs result at Information level
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Domain service result")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_RehydrationReadability_UsesCallerCancellationToken() {
        // Arrange
        var protectionService = new FakeUnreadableProtectionService();
        ActorTestContext ctx = CreateActor(protectionService);
        ConfigureNoDuplicate(ctx.StateManager);
        using var cts = new CancellationTokenSource();

        var metadata = new AggregateMetadata(1, DateTimeOffset.UtcNow, null);
        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata",
            Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        var evt = new EventEnvelope(
            "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json",
            [1, 2, 3], null);
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:1",
            Arg.Any<CancellationToken>())
            .Returns(_ => {
                cts.Cancel();
                return new ConditionalValue<EventEnvelope>(true, evt);
            });

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => ctx.Actor.ProcessCommandAsync(envelope, cts.Token));
        protectionService.EventUnprotectInvocations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessCommandAsync_UnknownEvent_DeadLettersAndRejectsCommand() {
        // Arrange -- metadata says 2 events but event 2 is missing (simulates gap)
        // Dead-letter routing handles infrastructure exceptions (MissingEventException).
        ActorTestContext ctx = CreateActor();
        _ = ctx.StateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        var metadata = new AggregateMetadata(2, DateTimeOffset.UtcNow, null);
        _ = ctx.StateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        var evt1 = new EventEnvelope("msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json", [1], null);
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt1));
        _ = ctx.StateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result instead of propagating
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_StateRehydration_LogsStateType() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 2);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("State rehydrated")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessCommandAsync_WithSnapshot_PassesSnapshotAwareCurrentStateToDomainService() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 2);

        _ = ctx.SnapshotManager.LoadSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>())
            .Returns(new SnapshotRecord(1, JsonSerializer.Deserialize<JsonElement>("""{"state":"snapshot-state"}"""), DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant") { SnapshotEnvelopeVersion = 1 });

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- domain invocation gets snapshot state plus tail events without forcing a second full replay
        _ = await ctx.Invoker.Received(1).InvokeAsync(
            Arg.Any<CommandEnvelope>(),
            Arg.Is<object?>(o => IsSnapshotAwareCurrentState(o)));
    }

    // === Story 3.5: Domain Service Invocation Tests ===

    [Fact]
    public async Task ProcessCommandAsync_DomainInvocation_UsesCallerCancellationToken() {
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        using var cancellation = new CancellationTokenSource();
        _ = ctx.Invoker.InvokeAsync(
                Arg.Any<CommandEnvelope>(),
                Arg.Any<object?>(),
                cancellation.Token)
            .Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        _ = await ctx.Actor.ProcessCommandAsync(envelope, cancellation.Token);

        _ = await ctx.Invoker.Received(1).InvokeAsync(
            envelope,
            Arg.Any<object?>(),
            cancellation.Token);
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_PersistsEventsViaStep5() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- Step 5 (EventPersister) writes event to state store
        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await ctx.StateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_ForwardsCallerCancellationToEventStaging() {
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));
        using var cancellation = new CancellationTokenSource();

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope(), cancellation.Token);

        result.Accepted.ShouldBeTrue();
        await ctx.StateManager.Received(1).SetStateAsync(
            Arg.Is<string>(key => key.Contains(":events:", StringComparison.Ordinal)),
            Arg.Any<EventEnvelope>(),
            cancellation.Token);
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ReturnsRejectionResult() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        _ = result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Domain rejection");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_StoresInIdempotencyCache() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope(causationId: "cause-reject");

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        await ctx.StateManager.Received(1).SetStateAsync(
            $"idempotency:{envelope.MessageId}",
            Arg.Is<IdempotencyRecord>(r => r.Accepted == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainNoOp_ReturnsAccepted() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainServiceNotFound_DeadLettersAndRejectsCommand() {
        // Arrange -- dead-letter routing handles infrastructure exceptions
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new DomainServiceNotFoundException("test-tenant", "test-domain"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInfrastructureFailure_DeadLettersAndRejectsCommand() {
        // Arrange -- dead-letter routing handles infrastructure exceptions
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act -- dead-letter routing returns Rejected result
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        string errorMessage = result.ErrorMessage.ShouldNotBeNull();
        errorMessage.ShouldContain("Protected data diagnostic details were redacted.");
        errorMessage.ShouldContain("ReasonCode=protected-data-diagnostic-redacted");
        errorMessage.ShouldContain("Stage=Processing");
        errorMessage.ShouldNotContain("Service unavailable");
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInfrastructureFailure_WritesRejectedStatusWithFailureReason() {
        // Arrange -- infrastructure failures must remain distinguishable from domain rejections
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        await ctx.StatusStore.Received().WriteStatusAsync(
            envelope.TenantId,
            envelope.MessageId,
            Arg.Is<CommandStatusRecord>(record =>
                record.Status == CommandStatus.Rejected
                && record.MessageId == envelope.MessageId
                && record.CorrelationId == envelope.CorrelationId
                && record.FailureReason == "Protected data diagnostic details were redacted. ReasonCode=protected-data-diagnostic-redacted; Stage=Processing."
                && record.RejectionEventType == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainInvocation_LogsResultType() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        ctx.Logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Domain service result") && o.ToString()!.Contains("Success")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // === Story 3.7: Event Persistence (Step 5) Tests ===

    [Fact]
    public async Task ProcessCommandAsync_AfterEventPersistence_IncludesTerminalAdvisoryAcknowledgement() {
        // Arrange -- Story 3.11: Processing checkpoint + EventsStored atomic commit + terminal commit
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Processing, EventsStored+events, and terminal+counter each commit once.
        // Confirmed advisory acknowledgement adds one save after the unchanged terminal batch.
        await ctx.StateManager.Received(4).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_SaveStateAsyncStillCalled() {
        // Arrange -- no-op still saves (idempotency record + pipeline cleanup)
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Admission and terminal cleanup each commit once; terminal cleanup owns the counter update.
        // Confirmed advisory acknowledgement adds one save after the unchanged terminal batch.
        await ctx.StateManager.Received(3).SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccess_ResultIncludesCorrectEventCount() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[]
        {
            new TestEvent(), new TestEvent(), new TestEvent(),
        });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(3);
    }

    [Fact]
    public async Task ProcessCommandAsync_NoOpResult_EventCountIsZero() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncThrows_RetriesAgainstFreshState() {
        // Arrange -- Story 3.8: allow Processing checkpoint SaveStateAsync to succeed,
        // then throw once on the EventsStored batch commit and retry from rehydration.
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext ctx = CreateActor(stateManager: stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        stateManager.FaultOnCall("SaveState", 2, new InvalidOperationException("ETag mismatch"));

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-conflict");

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeTrue();
        _ = await ctx.Invoker.Received(2).InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
        stateManager.Trace.Count(operation => operation == "ClearCache").ShouldBe(3);
    }

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncConflictRetryExhausted_ReturnsRejectedConcurrencyResult() {
        // Arrange -- zero configured retries means the first EventsStored commit conflict is terminal.
        var stateManager = new FaultInjectingActorStateManager();
        ActorTestContext ctx = CreateActor(
            concurrencyOptions: new CommandConcurrencyOptions { MaxPersistenceConflictRetries = 0 },
            stateManager: stateManager);
        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        stateManager.FaultOnCall("SaveState", 2, new InvalidOperationException("ETag mismatch"));

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: "corr-conflict-terminal", causationId: "cause-conflict-terminal");

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("ConcurrencyConflict");
        IReadOnlyDictionary<string, object> rejectedState = stateManager.CreateCommittedView();
        rejectedState.ShouldNotContainKey($"idempotency:{envelope.MessageId}");
        rejectedState.ShouldNotContainKey($"{envelope.AggregateIdentity.EventStreamKeyPrefix}1");
        rejectedState.ShouldNotContainKey($"{envelope.AggregateIdentity.PipelineKeyPrefix}{envelope.CorrelationId}");
        await ctx.StatusStore.Received(1).WriteStatusAsync(
            envelope.TenantId,
            envelope.MessageId,
            Arg.Is<CommandStatusRecord>(record =>
                record.Status == CommandStatus.Rejected
                && record.MessageId == envelope.MessageId
                && record.CorrelationId == envelope.CorrelationId
                && record.FailureReason == "ConcurrencyConflict"
                && record.RejectionEventType == null),
            Arg.Any<CancellationToken>());
        _ = await ctx.EventPublisher.DidNotReceive().PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());

        CommandProcessingResult retry = await ctx.Actor.ProcessCommandAsync(envelope);

        retry.Accepted.ShouldBeTrue();
        _ = await ctx.Invoker.Received(2).InvokeAsync(envelope, Arg.Any<object?>());
    }

    // === Code Review Fix: D3 -- Rejection events persisted ===

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_PersistsRejectionEventsViaEventPersister() {
        // Arrange -- D3: rejection events are events, they must be persisted
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert -- EventPersister should have written rejection event to state store
        await ctx.StateManager.Received().SetStateAsync(
            Arg.Is<string>(s => s.Contains(":events:")),
            Arg.Any<EventEnvelope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejection_ResultIncludesEventCount() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        // Assert
        result.EventCount.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessCommandAsync_SaveStateAsyncThrowsOperationCanceled_PropagatesDirectly() {
        // Arrange -- OperationCanceledException should NOT be wrapped as ConcurrencyConflictException
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(DomainResult.NoOp());

        _ = ctx.StateManager.SaveStateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException("Shutdown")));

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act & Assert -- should propagate directly, not wrapped
        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => ctx.Actor.ProcessCommandAsync(envelope));
    }

    // === Folded snapshot actor tests ===

    [Fact]
    public async Task ProcessCommandAsync_QualifiedRequestedFoldStagesPersistedHead() {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        DomainResult result = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() })
            with { PostCommandStateJson = """{"count":4}""" };
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(result);

        CommandProcessingResult processed = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        processed.Accepted.ShouldBeTrue();
        _ = await ctx.Invoker.Received(1).InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
            "test-profile", Arg.Any<CancellationToken>());
        await ctx.SnapshotManager.Received(1).CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            4,
            Arg.Is<object>(state => IsFourCountFold(state)),
            ctx.StateManager,
            Arg.Any<JsonSerializerOptions>(),
            Arg.Any<FoldedSnapshotCaptureLimits>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>(),
            false);
    }

    [Fact]
    public async Task ProcessCommandAsync_InvalidQualificationRegistrySkipsCaptureButCommitsEvents()
    {
        FoldedSnapshotQualification duplicate = new("test-domain", "test-domain", typeof(JsonElement),
            "test-profile", "test-parity", SnapshotTestLimits.ForState());
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(duplicate)
            .AddSingleton(duplicate)
            .AddSingleton<FoldedSnapshotQualificationRegistry>()
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await ctx.SnapshotManager.DidNotReceive().ShouldCreateSnapshotAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(),
            Arg.Any<long>(), Arg.Any<CancellationToken>());
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            "FailureClass=SnapshotCaptureUnsupported", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("AddressMismatch")]
    [InlineData("SnapshotSequenceInvalid")]
    [InlineData("SnapshotStateInvalid")]
    [InlineData("SnapshotStateShapeCollision")]
    [InlineData("CorruptSnapshot")]
    [InlineData("SnapshotReadUnavailable")]
    [InlineData("SnapshotUnprotectUnavailable")]
    public async Task ProcessCommandAsync_RetainedSnapshotFailureIsReplacedAtCommittedHead(string reason)
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        IEventPayloadProtectionService protection = reason == "SnapshotUnprotectUnavailable"
            ? Substitute.For<IEventPayloadProtectionService>() : new NoOpEventPayloadProtectionService();
        if (reason == "SnapshotUnprotectUnavailable")
        {
            _ = protection.TryUnprotectSnapshotAsync(
                    Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
                    Arg.Any<object>(), Arg.Any<Hexalith.EventStore.Contracts.Security.EventStorePayloadProtectionMetadata>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromException<Hexalith.EventStore.Contracts.Security.SnapshotUnprotectionOutcome>(
                    new IOException("provider detail")));
            _ = protection.ProtectSnapshotAsync(
                    Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
                    Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(call => new Hexalith.EventStore.Contracts.Security.SnapshotProtectionResult(
                    call.ArgAt<object>(1),
                    Hexalith.EventStore.Contracts.Security.EventStorePayloadProtectionMetadata.Unprotected()));
        }
        ILogger<SnapshotManager> managerLogger = Substitute.For<ILogger<SnapshotManager>>();
        var manager = new SnapshotManager(Microsoft.Extensions.Options.Options.Create(new SnapshotOptions()),
            managerLogger, protection);
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger,
            snapshotManager: manager);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 100);
        if (reason == "SnapshotReadUnavailable")
        {
            _ = ctx.StateManager.TryGetStateAsync<object>(
                    "test-tenant:test-domain:agg-001:snapshot", Arg.Any<CancellationToken>())
                .Returns(Task.FromException<ConditionalValue<object>>(new IOException("provider detail")));
        }
        else if (reason == "SnapshotUnprotectUnavailable")
        {
            _ = ctx.StateManager.TryGetStateAsync<object>(
                    "test-tenant:test-domain:agg-001:snapshot", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<object>(true, JsonSerializer.Deserialize<JsonElement>(
                    """{"snapshotEnvelopeVersion":1,"sequenceNumber":100,"state":{"count":100},"createdAt":"2026-01-01T00:00:00Z","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant"}""")));
        }
        else
        {
            string raw = reason switch
            {
                "AddressMismatch" => """{"snapshotEnvelopeVersion":1,"sequenceNumber":100,"state":{"count":100},"createdAt":"2026-01-01T00:00:00Z","domain":"test-domain","aggregateId":"wrong","tenantId":"test-tenant"}""",
                "SnapshotSequenceInvalid" => """{"snapshotEnvelopeVersion":1,"sequenceNumber":102,"state":{"count":100},"createdAt":"2026-01-01T00:00:00Z","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant"}""",
                "SnapshotStateShapeCollision" => """{"snapshotEnvelopeVersion":1,"sequenceNumber":100,"state":{"currentSequence":100,"events":[]},"createdAt":"2026-01-01T00:00:00Z","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant"}""",
                "CorruptSnapshot" => """{"snapshotEnvelopeVersion":1,"sequenceNumber":100,"state":{"count":100},"createdAt":"bad","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant","protectionMetadata":{"state":"Protected","metadataVersion":1,"providerId":"test-provider","keyReference":"test-key"}}""",
                _ => """{"snapshotEnvelopeVersion":1,"sequenceNumber":100,"state":null,"createdAt":"2026-01-01T00:00:00Z","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant"}""",
            };
            _ = ctx.StateManager.TryGetStateAsync<object>(
                    "test-tenant:test-domain:agg-001:snapshot", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<object>(true, JsonSerializer.Deserialize<JsonElement>(raw)));
        }
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]) with
            {
                PostCommandStateJson = """{"count":101}""",
            });

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        await ctx.StateManager.Received(1).SetStateAsync(
            "test-tenant:test-domain:agg-001:snapshot",
            Arg.Is<SnapshotRecord>(snapshot => snapshot.SequenceNumber == 101
                && snapshot.SnapshotEnvelopeVersion == 1), Arg.Any<CancellationToken>());
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            $"ReplacementReasonClass={reason}", StringComparison.Ordinal));
        if (reason == "SnapshotUnprotectUnavailable")
        {
            NSubstitute.Core.ICall[] calls = ctx.StateManager.ReceivedCalls().ToArray();
            int snapshotStageIndex = Array.FindIndex(calls, call =>
                call.GetMethodInfo().Name == "SetStateAsync"
                && call.GetArguments()[0] is string key
                && key == "test-tenant:test-domain:agg-001:snapshot");
            snapshotStageIndex.ShouldBeGreaterThanOrEqualTo(0);
            calls.Skip(snapshotStageIndex + 1).Any(call =>
                call.GetMethodInfo().Name == "SaveStateAsync").ShouldBeTrue();
            snapshotLogger.Messages.ShouldNotContain(message => message.Contains(
                "provider detail", StringComparison.Ordinal));
            NSubstitute.Core.ICall[] managerLogCalls = managerLogger.ReceivedCalls()
                .Where(call => call.GetArguments().Length > 3).ToArray();
            managerLogCalls.ShouldNotBeEmpty();
            managerLogCalls
                .All(call => call.GetArguments()[3] is null
                    && !call.GetArguments()[2]!.ToString()!.Contains("provider detail",
                        StringComparison.Ordinal)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task AutomaticReplacementRejectsAnUnlistedReason()
    {
        var manager = new SnapshotManager(Microsoft.Extensions.Options.Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        var stateManager = new InMemoryStateManager();
        _ = await Should.ThrowAsync<ArgumentOutOfRangeException>(() => manager.CreateAutomaticReplacementAsync(
            CreateTestEnvelope().AggregateIdentity, 1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
            JsonSerializerOptions.Web, SnapshotTestLimits.ForState(), "UnlistedReason", null,
            CancellationToken.None));
        (await stateManager.ContainsStateAsync("test-tenant:test-domain:agg-001:snapshot")).ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCommandAsync_QualifiedRejectionUsesLoadedSequenceAndResolvedTypeAtBothDueChecks()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "OrderAggregate", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.AggregateTypeResolver.ResolveAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>())
            .Returns("OrderAggregate");
        _ = ctx.SnapshotManager.LoadSnapshotAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
                ctx.StateManager, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new SnapshotRecord(2, JsonSerializer.Deserialize<JsonElement>("""{"count":2}"""),
                DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant")
                { SnapshotEnvelopeVersion = 1 });
        using var cancellation = new CancellationTokenSource();
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                "test-tenant", "test-domain", "OrderAggregate", Arg.Any<long>(),
                Arg.Any<long>(), cancellation.Token)
            .Returns(true);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", cancellation.Token)
            .Returns(DomainResult.Rejection([new TestRejectionEvent()]) with
            {
                PostCommandStateJson = """{"count":4}""",
            });

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope(), cancellation.Token);

        result.EventCount.ShouldBe(1);
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync(
            "test-tenant", "test-domain", "OrderAggregate", 3, 2, cancellation.Token);
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync(
            "test-tenant", "test-domain", "OrderAggregate", 4, 2, cancellation.Token);
        await ctx.SnapshotManager.Received(1).CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), 4,
            Arg.Is<object>(state => IsFourCountFold(state)), ctx.StateManager, Arg.Any<JsonSerializerOptions>(),
            Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(), cancellation.Token, false);
    }

    [Fact]
    public async Task ProcessCommandAsync_FirstIntervalCrossingDoesNotRequestUnqualifiedCapture() {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 99);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<long>(3) - call.ArgAt<long>(4) >= 100);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]));

        CommandProcessingResult processed = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        processed.Accepted.ShouldBeTrue();
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync(
            "test-tenant", "test-domain", "test-domain", 99, 0, Arg.Any<CancellationToken>());
        _ = await ctx.Invoker.DidNotReceive().InvokeAsync(
            Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessCommandAsync_QualifiedNoOpOrMissingEvidenceNeverStagesSnapshot(bool noOp) {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        DomainResult result = noOp ? DomainResult.NoOp() : DomainResult.Success([new TestEvent()]);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(result);

        CommandProcessingResult processed = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        processed.Accepted.ShouldBeTrue();
        _ = await ctx.Invoker.Received(1).InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
            "test-profile", Arg.Any<CancellationToken>());
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
        if (!noOp)
        {
            snapshotLogger.Messages.ShouldContain(message => message.Contains(
                "FailureClass=SnapshotEvidenceAbsent", StringComparison.Ordinal));
        }
        snapshotLogger.Exceptions.ShouldAllBe(exception => exception == null);
    }

    [Fact]
    public async Task ProcessCommandAsync_AmbiguousStateEvidenceEmitsOnlySafeCollisionClass()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]) with
            {
                PostCommandStateJson = """{"currentSequence":4,"events":[],"secret":"sensitive-value"}""",
            });

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            "FailureClass=SnapshotStateShapeCollision", StringComparison.Ordinal));
        snapshotLogger.Messages.ShouldNotContain(message => message.Contains("sensitive-value", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"count\":4", "SnapshotStateInvalid")]
    [InlineData("{\"count\":4,\"Count\":5}", "SnapshotStateInvalid")]
    [InlineData("{\"nested\":{\"count\":4,\"count\":5}}", "SnapshotStateInvalid")]
    [InlineData("{\"items\":[{\"count\":4,\"count\":5}]}", "SnapshotStateInvalid")]
    [InlineData("over-depth", "SnapshotStateInvalid")]
    public async Task ProcessCommandAsync_InvalidPresentEvidenceSkipsSnapshotWithSafeReason(
        string evidence, string reason)
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        string presentEvidence = evidence == "over-depth"
            ? "{\"count\":" + new string('[', 70) + "0" + new string(']', 70) + "}"
            : evidence;
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]) with { PostCommandStateJson = presentEvidence });

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        await ctx.StateManager.Received().SetStateAsync(
            "test-tenant:test-domain:agg-001:events:4", Arg.Any<EventEnvelope>(), Arg.Any<CancellationToken>());
        await ctx.StateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            $"FailureClass={reason}", StringComparison.Ordinal));
        snapshotLogger.Messages.ShouldNotContain(message => message.Contains(
            "FailureClass=SnapshotEvidenceAbsent", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"nested\":{\"count\":11},\"items\":[{\"count\":12}]}")]
    [InlineData("{\"nested\":{\"count\":11,\"Count\":12},\"items\":[{\"count\":13,\"Count\":14}]}")]
    public async Task ProcessCommandAsync_AdmittedNestedStateRemainsReadableAfterSnapshotStaging(string evidence)
    {
        // HX-FS-V1 §§5/9, V7/V17: write and read use the same recursive property-name validator.
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        var snapshots = new SnapshotManager(Microsoft.Extensions.Options.Options.Create(new SnapshotOptions { DefaultInterval = 10 }),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        ActorTestContext ctx = CreateActor(serviceProvider: services, snapshotManager: snapshots);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 10);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()]) with { PostCommandStateJson = evidence });
        SnapshotRecord? staged = null;
        _ = ctx.StateManager.SetStateAsync("test-tenant:test-domain:agg-001:snapshot",
                Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => { staged = call.ArgAt<SnapshotRecord>(1); return Task.CompletedTask; });

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        staged.ShouldNotBeNull().SequenceNumber.ShouldBe(11);
        await ctx.StateManager.Received().SaveStateAsync(Arg.Any<CancellationToken>());
        _ = ctx.StateManager.TryGetStateAsync<object>("test-tenant:test-domain:agg-001:snapshot", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<object>(true, staged));
        SnapshotLoadResult readback = await snapshots.LoadSnapshotForCommandAsync(
            CreateTestEnvelope().AggregateIdentity, ctx.StateManager);
        readback.Outcome.ShouldBe(SnapshotLoadOutcome.Readable);
        JsonElement.DeepEquals(readback.Snapshot.ShouldNotBeNull().State.ShouldBeOfType<JsonElement>(),
            JsonSerializer.Deserialize<JsonElement>(evidence)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessCommandAsync_CallerCancellationAtEitherSnapshotPolicyCheckPropagates(bool afterInvocation) {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        using var cancellation = new CancellationTokenSource();
        int policyCalls = 0;
        int savesBeforeCancellation = -1;
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                cancellation.Token)
            .Returns(_ =>
            {
                if (++policyCalls != (afterInvocation ? 2 : 1)) { return Task.FromResult(true); }
                savesBeforeCancellation = ctx.StateManager.ReceivedCalls()
                    .Count(call => call.GetMethodInfo().Name == "SaveStateAsync");
                cancellation.Cancel();
                return Task.FromCanceled<bool>(cancellation.Token);
            });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", cancellation.Token)
            .Returns(DomainResult.Success([new TestEvent()])
                with { PostCommandStateJson = """{"count":4}""" });

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            ctx.Actor.ProcessCommandAsync(CreateTestEnvelope(), cancellation.Token));

        policyCalls.ShouldBe(afterInvocation ? 2 : 1);
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
        ctx.StateManager.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "SaveStateAsync")
            .ShouldBe(savesBeforeCancellation);
    }

    [Fact]
    public async Task ProcessCommandAsync_CallerCancellationAfterSnapshotStagingDoesNotSaveTheBatch()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        using var cancellation = new CancellationTokenSource();
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                "test-tenant", "test-domain", "test-domain", Arg.Any<long>(), Arg.Any<long>(),
                cancellation.Token)
            .Returns(true);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", cancellation.Token)
            .Returns(DomainResult.Success([new TestEvent()]) with
            {
                PostCommandStateJson = """{"count":4}""",
            });
        int savesAtStaging = -1;
        _ = ctx.SnapshotManager.CreateSnapshotAsync(
                Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), 4,
                Arg.Any<object>(), ctx.StateManager, Arg.Any<JsonSerializerOptions>(),
                Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(), cancellation.Token, false)
            .Returns(async _ =>
            {
                await ctx.StateManager.SetStateAsync("test-tenant:test-domain:agg-001:snapshot",
                    new SnapshotRecord(4, JsonSerializer.Deserialize<JsonElement>("""{"count":4}"""),
                        DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant"),
                    cancellation.Token);
                savesAtStaging = ctx.StateManager.ReceivedCalls()
                    .Count(call => call.GetMethodInfo().Name == "SaveStateAsync");
                cancellation.Cancel();
            });

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            ctx.Actor.ProcessCommandAsync(CreateTestEnvelope(), cancellation.Token));

        savesAtStaging.ShouldBeGreaterThanOrEqualTo(0);
        ctx.StateManager.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "SaveStateAsync")
            .ShouldBe(savesAtStaging);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ProcessCommandAsync_SnapshotPolicyFailureDoesNotRejectValidEvents(bool afterInvocation, bool cancelInternally) {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        var snapshotLogger = new SnapshotCaptureLogger();
        ActorTestContext ctx = CreateActor(serviceProvider: services, logger: snapshotLogger);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        int policyCalls = 0;
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ++policyCalls == 1 && afterInvocation
                ? Task.FromResult(true)
                : Task.FromException<bool>(cancelInternally
                    ? new OperationCanceledException("policy canceled internally")
                    : new InvalidOperationException("policy offline")));
        DomainResult result = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() })
            with { PostCommandStateJson = """{"count":4}""" };
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(result);
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(result with { PostCommandStateJson = null });

        CommandProcessingResult processed = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        processed.Accepted.ShouldBeTrue();
        policyCalls.ShouldBe(afterInvocation ? 2 : 1);
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            "FailureClass=SnapshotPolicyUnavailable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessCommandAsync_ActorNodeLimitSkipsOnlyOptionalSnapshot() {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState(1024, nodeCount: 2)),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        DomainResult result = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() })
            with { PostCommandStateJson = """{"count":4}""" };
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(result);

        CommandProcessingResult processed = await ctx.Actor.ProcessCommandAsync(CreateTestEnvelope());

        processed.Accepted.ShouldBeTrue();
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainSuccessWithoutQualificationSkipsSnapshotPolicy() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        _ = await ctx.SnapshotManager.DidNotReceive().ShouldCreateSnapshotAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ResolvedAggregateTypeWithoutQualificationSkipsSnapshotPolicy() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);
        _ = ctx.AggregateTypeResolver.ResolveAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>())
            .Returns("OrderAggregate");

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        _ = await ctx.SnapshotManager.DidNotReceive().ShouldCreateSnapshotAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_WithoutQualificationDoesNotStageLegacyPreEventState() {
        // Arrange
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>())
            .Returns(true);

        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ShouldSnapshotFalse_DoesNotCallCreateSnapshot() {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification("test-domain", "test-domain", typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();
        ActorTestContext ctx = CreateActor(serviceProvider: services);
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        DomainResult successResult = DomainResult.Success([new TestEvent()])
            with { PostCommandStateJson = """{"count":4}""" };
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(successResult);
        _ = ctx.SnapshotManager.ShouldCreateSnapshotAsync(
                "test-tenant", "test-domain", "test-domain", 3, 0,
                Arg.Any<CancellationToken>())
            .Returns(false);

        CommandEnvelope envelope = CreateTestEnvelope();

        CommandProcessingResult result = await ctx.Actor.ProcessCommandAsync(envelope);

        result.Accepted.ShouldBeTrue();
        result.EventCount.ShouldBe(1);
        _ = await ctx.SnapshotManager.Received(1).ShouldCreateSnapshotAsync(
            "test-tenant", "test-domain", "test-domain", 3, 0, Arg.Any<CancellationToken>());
        _ = await ctx.Invoker.DidNotReceive().InvokeAsync(
            Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await ctx.SnapshotManager.DidNotReceive().CreateSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<long>(), Arg.Any<object>(), Arg.Any<IActorStateManager>(),
            Arg.Any<JsonSerializerOptions>(), Arg.Any<FoldedSnapshotCaptureLimits>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ProcessCommandAsync_ExistingSnapshotWithoutQualificationSkipsSnapshotPolicy() {
        // Existing snapshot is unqualified, so no optional policy check runs.
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var existingSnapshot = new SnapshotRecord(100, new object(), DateTimeOffset.UtcNow, "test-domain", "agg-001", "test-tenant");
        _ = ctx.SnapshotManager.LoadSnapshotAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IActorStateManager>(),
            Arg.Any<string?>())
            .Returns(existingSnapshot);

        var successResult = DomainResult.Success(new Hexalith.EventStore.Contracts.Events.IEventPayload[] { new TestEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(successResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        _ = await ctx.SnapshotManager.DidNotReceive().ShouldCreateSnapshotAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCommandAsync_DomainRejectionWithoutQualificationSkipsSnapshotPolicy() {
        // Rejection is unqualified, so no optional policy check runs.
        ActorTestContext ctx = CreateActor();
        ConfigureNoDuplicate(ctx.StateManager);
        ConfigureExistingAggregate(ctx.StateManager, 3);

        var rejectionResult = DomainResult.Rejection(new Hexalith.EventStore.Contracts.Events.IRejectionEvent[] { new TestRejectionEvent() });
        _ = ctx.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>()).Returns(rejectionResult);
        CommandEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await ctx.Actor.ProcessCommandAsync(envelope);

        _ = await ctx.SnapshotManager.DidNotReceive().ShouldCreateSnapshotAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private static bool IsFourCountFold(object state)
        => state is JsonElement element && element.GetProperty("count").GetInt32() == 4;

}
