
using System.Diagnostics;
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Telemetry;
/// <summary>
/// Story 6.1 Task 8: End-to-end trace activity tests.
/// Verifies that the AggregateActor pipeline creates correct activities with proper tags and status codes.
/// </summary>
public class EndToEndTraceTests {
    private static (AggregateActor Actor, IActorStateManager StateManager, IDomainServiceInvoker Invoker, IEventPublisher Publisher, IDeadLetterPublisher DeadLetterPublisher)
        CreateActorWithMockState(string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, commandStatusStore,
            eventPublisher, Options.Create(new EventDrainOptions()),
            deadLetterPublisher);

        // Inject StateManager via reflection (Dapr framework normally handles this)
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Configure default: no duplicates, no pipeline state, no metadata
        _ = stateManager.TryGetStateAsync<CommandProcessingResult>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<CommandProcessingResult>(false, default!));
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: domain service returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        // Default: snapshot not found
        _ = snapshotManager.LoadSnapshotAsync(Arg.Any<Contracts.Identity.AggregateIdentity>(), Arg.Any<IActorStateManager>(), Arg.Any<string>())
            .Returns((SnapshotRecord?)null);

        // Default: event rehydration returns empty
        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 0, null));

        return (actor, stateManager, invoker, eventPublisher, deadLetterPublisher);
    }

    private static CommandEnvelope CreateTestEnvelope(string correlationId = "corr-trace-test") => new(
        TenantId: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId,
        CausationId: null,
        UserId: "system",
        Extensions: null);

    [Fact]
    public async Task ProcessCommand_CreatesProcessCommandActivity() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.ProcessCommand
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState();
        CommandEnvelope command = CreateTestEnvelope(correlationId);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert
        _ = capturedActivity.ShouldNotBeNull("ProcessCommand activity should be created");
        capturedActivity.OperationName.ShouldBe(EventStoreActivitySource.ProcessCommand);
    }

    [Fact]
    public async Task ProcessCommand_NoAmbientActivity_UsesTraceparentFallbackFromExtensions() {
        // Arrange
        string correlationId = $"trace-fallback-{Guid.NewGuid()}";
        var traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        string traceParent = $"00-{traceId.ToHexString()}-{parentSpanId.ToHexString()}-01";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.ProcessCommand
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState();
        var command = new CommandEnvelope(
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: correlationId,
            CausationId: null,
            UserId: "system",
            Extensions: new Dictionary<string, string> { ["traceparent"] = traceParent });

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.TraceId.ShouldBe(traceId);
        capturedActivity.ParentSpanId.ShouldBe(parentSpanId);
    }

    [Fact]
    public async Task ProcessCommand_CreatesChildActivitiesForEachStage() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        List<Activity> capturedActivities = [];

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState();
        CommandEnvelope command = CreateTestEnvelope(correlationId);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert -- all 5 pipeline stages should create activities (NoOp path: idempotency, tenant, rehydration, domain invoke, then terminal)
        string[] expectedActivities = [
            EventStoreActivitySource.ProcessCommand,
            EventStoreActivitySource.IdempotencyCheck,
            EventStoreActivitySource.TenantValidation,
            EventStoreActivitySource.StateRehydration,
            EventStoreActivitySource.DomainServiceInvoke,
        ];

        foreach (string expected in expectedActivities) {
            capturedActivities.ShouldContain(
                a => a.OperationName == expected,
                $"Activity '{expected}' should be created");
        }
    }

    [Fact]
    public async Task ProcessCommand_ActivitiesHaveCorrelationIdTag() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        List<Activity> capturedActivities = [];

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState();
        CommandEnvelope command = CreateTestEnvelope(correlationId);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert -- every captured activity has the correlation ID tag
        capturedActivities.Count.ShouldBeGreaterThan(0);
        foreach (Activity activity in capturedActivities) {
            activity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldBe(correlationId);
        }
    }

    [Fact]
    public async Task ProcessCommand_ActivitiesHaveTenantIdTag() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        List<Activity> capturedActivities = [];

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState();
        CommandEnvelope command = CreateTestEnvelope(correlationId);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert -- every captured activity has the tenant ID tag
        capturedActivities.Count.ShouldBeGreaterThan(0);
        foreach (Activity activity in capturedActivities) {
            activity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("test-tenant");
        }
    }

    [Fact]
    public async Task ProcessCommand_SuccessfulCommand_SetsOkStatus() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        Activity? processActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.ProcessCommand
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    processActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState();
        CommandEnvelope command = CreateTestEnvelope(correlationId);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert
        _ = processActivity.ShouldNotBeNull();
        processActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task ProcessCommand_TenantMismatch_SetsErrorStatus() {
        // Arrange -- actor ID tenant != command tenant
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        Activity? processActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.ProcessCommand
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    processActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Actor ID uses "test-tenant" but command uses "wrong-tenant"
        (AggregateActor actor, _, _, _, _) = CreateActorWithMockState("test-tenant:test-domain:agg-001");
        var command = new CommandEnvelope(
            TenantId: "wrong-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: correlationId,
            CausationId: null,
            UserId: "system",
            Extensions: null);

        // Act
        _ = await actor.ProcessCommandAsync(command);

        // Assert
        _ = processActivity.ShouldNotBeNull();
        processActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task EventPublisher_CreatesPublishActivity() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.EventsPublish
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        DaprClient daprClient = Substitute.For<Dapr.Client.DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions { PubSubName = "pubsub" });
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        var publisher = new EventPublisher(daprClient, options, logger);

        var identity = new Contracts.Identity.AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var events = new List<EventEnvelope>
        {
            new(
                AggregateId: "agg-001",
                TenantId: "test-tenant",
                Domain: "test-domain",
                SequenceNumber: 1,
                Timestamp: DateTimeOffset.UtcNow,
                CorrelationId: correlationId,
                CausationId: "cmd-001",
                UserId: "system",
                DomainServiceVersion: "1.0",
                EventTypeName: "OrderCreated",
                SerializationFormat: "json",
                Payload: [1, 2, 3],
                Extensions: null),
        };

        // Act
        EventPublishResult result = await publisher
            .PublishEventsAsync(identity, events, correlationId);

        // Assert
        result.Success.ShouldBeTrue();
        _ = capturedActivity.ShouldNotBeNull("Publish activity should be created");
        capturedActivity.OperationName.ShouldBe(EventStoreActivitySource.EventsPublish);
        capturedActivity.Kind.ShouldBe(ActivityKind.Producer);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task DeadLetterPublisher_CreatesDeadLetterActivity() {
        // Arrange
        string correlationId = $"trace-test-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.EventsPublishDeadLetter
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        DaprClient daprClient = Substitute.For<Dapr.Client.DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions { PubSubName = "pubsub" });
        ILogger<DeadLetterPublisher> logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new Contracts.Identity.AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var commandEnvelope = new CommandEnvelope(
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            Payload: [1, 2, 3],
            CorrelationId: correlationId,
            CausationId: null,
            UserId: "system",
            Extensions: null);
        var message = new DeadLetterMessage(
            Command: commandEnvelope,
            FailureStage: "Processing",
            ExceptionType: "InvalidOperationException",
            ErrorMessage: "Test error",
            CorrelationId: correlationId,
            CausationId: null,
            TenantId: "test-tenant",
            Domain: "test-domain",
            AggregateId: "agg-001",
            CommandType: "CreateOrder",
            FailedAt: DateTimeOffset.UtcNow,
            EventCountAtFailure: null);

        // Act
        _ = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        _ = capturedActivity.ShouldNotBeNull("DeadLetter activity should be created");
        capturedActivity.OperationName.ShouldBe(EventStoreActivitySource.EventsPublishDeadLetter);
        capturedActivity.Kind.ShouldBe(ActivityKind.Producer);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }
}
