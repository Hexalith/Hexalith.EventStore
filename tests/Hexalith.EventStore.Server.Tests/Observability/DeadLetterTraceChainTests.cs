
using System.Diagnostics;
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Telemetry;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Observability;
/// <summary>
/// Story 6.3 Task 4: Trace-based dead-letter-to-origin tracing tests.
/// Verifies that OpenTelemetry activities form an unbroken trace chain from command receipt
/// through failure to dead-letter publication, with correlation ID tags on every activity.
/// </summary>
public class DeadLetterTraceChainTests {
    #region Shared Helpers

    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001",
        string? correlationId = null,
        Dictionary<string, string>? extensions = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: null,
        UserId: "system",
        Extensions: extensions);

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker, FakeDeadLetterPublisher DeadLetterPublisher)
        CreateActorWithFakeDeadLetter(string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var fakeDeadLetter = new FakeDeadLetterPublisher();

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore,
            eventPublisher, Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            fakeDeadLetter);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: no duplicates, no pipeline state, no metadata
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Default: domain service returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        // Default: snapshot not found
        _ = snapshotManager.LoadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IActorStateManager>(), Arg.Any<string>())
            .Returns((SnapshotRecord?)null);

        // Default: event rehydration returns empty
        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 0, null));

        return (actor, stateManager, logger, invoker, fakeDeadLetter);
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker, IDeadLetterPublisher MockDeadLetterPublisher)
        CreateActorWithMockDeadLetter(string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();

        // Default: dead-letter publication succeeds
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore,
            eventPublisher, Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        _ = snapshotManager.LoadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IActorStateManager>(), Arg.Any<string>())
            .Returns((SnapshotRecord?)null);

        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 0, null));

        return (actor, stateManager, logger, invoker, deadLetterPublisher);
    }

    private static (AggregateActor Actor, IActorStateManager StateManager, ILogger<AggregateActor> Logger, IDomainServiceInvoker Invoker)
        CreateActorWithRealDeadLetter(string actorId = "test-tenant:test-domain:agg-001") {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();

        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions { PubSubName = "pubsub" });
        ILogger<DeadLetterPublisher> deadLetterLogger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var deadLetterPublisher = new DeadLetterPublisher(daprClient, options, deadLetterLogger);

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(actorId) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore,
            eventPublisher, Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(DomainResult.NoOp());

        _ = snapshotManager.LoadSnapshotAsync(Arg.Any<AggregateIdentity>(), Arg.Any<IActorStateManager>(), Arg.Any<string>())
            .Returns((SnapshotRecord?)null);

        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));

        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new EventPublishResult(true, 0, null));

        return (actor, stateManager, logger, invoker);
    }

    /// <summary>
    /// Extracts all log messages from an NSubstitute ILogger mock by inspecting received calls.
    /// </summary>
    private static IReadOnlyList<(LogLevel Level, string Message)> GetLogEntries(ILogger logger) {
        var entries = new List<(LogLevel Level, string Message)>();
        foreach (NSubstitute.Core.ICall call in logger.ReceivedCalls()) {
            if (call.GetMethodInfo().Name == "Log" && call.GetArguments().Length >= 5) {
                object?[] args = call.GetArguments();
                var level = (LogLevel)args[0]!;
                object? state = args[2];
                var exception = args[3] as Exception;
                object? formatter = args[4];
                string? message = null;
                if (formatter is not null && state is not null) {
                    try {
                        MethodInfo? invokeMethod = formatter.GetType().GetMethod("Invoke");
                        message = invokeMethod?.Invoke(formatter, [state, exception])?.ToString();
                    }
                    catch {
                        message = state.ToString();
                    }
                }

                message ??= state?.ToString() ?? string.Empty;
                entries.Add((level, message));
            }
        }

        return entries;
    }

    #endregion

    #region Task 4.2: DomainServiceFailure_TraceSpansEntireLifecycle

    [Fact]
    public async Task DomainServiceFailure_TraceSpansEntireLifecycle() {
        // Arrange
        string correlationId = $"trace-chain-{Guid.NewGuid()}";
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

        (AggregateActor actor, _, _, IDomainServiceInvoker invoker) = CreateActorWithRealDeadLetter();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated domain service failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Verify activities span the lifecycle up to the failing stage
        string[] expectedActivities =
        [
            EventStoreActivitySource.ProcessCommand,
            EventStoreActivitySource.IdempotencyCheck,
            EventStoreActivitySource.TenantValidation,
            EventStoreActivitySource.StateRehydration,
            EventStoreActivitySource.DomainServiceInvoke,
            EventStoreActivitySource.EventsPublishDeadLetter,
        ];

        foreach (string expected in expectedActivities) {
            capturedActivities.ShouldContain(
                a => a.OperationName == expected,
                $"Activity '{expected}' should be created in the failure lifecycle");
        }

        // All activities should share the same trace ID
        ActivityTraceId traceId = capturedActivities[0].TraceId;
        foreach (Activity activity in capturedActivities) {
            activity.TraceId.ShouldBe(traceId, $"Activity '{activity.OperationName}' should share the same trace ID");
        }
    }

    [Fact]
    public async Task DomainServiceFailure_ApiToActorToDeadLetter_UsesSingleTraceId() {
        // Arrange
        string correlationId = $"trace-full-{Guid.NewGuid()}";
        List<Activity> capturedActivities = [];

        using var listener = new ActivityListener {
            ShouldListenTo = source =>
                source.Name is EventStoreActivitySource.SourceName
                or "Hexalith.EventStore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    capturedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, IDomainServiceInvoker invoker) = CreateActorWithRealDeadLetter();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated domain service failure"));

        // Act: create API submit span, then run actor processing
        using (Activity? apiActivity = EventStoreActivitySources.EventStore.StartActivity(EventStoreActivitySources.Submit, ActivityKind.Server)) {
            _ = apiActivity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
            _ = apiActivity?.SetTag(EventStoreActivitySource.TagTenantId, envelope.TenantId);
            _ = apiActivity?.SetTag(EventStoreActivitySource.TagDomain, envelope.Domain);
            _ = apiActivity?.SetTag(EventStoreActivitySource.TagCommandType, envelope.CommandType);

            _ = await actor.ProcessCommandAsync(envelope);
            _ = apiActivity?.SetStatus(ActivityStatusCode.Ok);
        }

        // Assert
        capturedActivities.ShouldContain(a => a.OperationName == EventStoreActivitySources.Submit);
        capturedActivities.ShouldContain(a => a.OperationName == EventStoreActivitySource.ProcessCommand);
        capturedActivities.ShouldContain(a => a.OperationName == EventStoreActivitySource.DomainServiceInvoke);
        capturedActivities.ShouldContain(a => a.OperationName == EventStoreActivitySource.EventsPublishDeadLetter);

        ActivityTraceId traceId = capturedActivities[0].TraceId;
        foreach (Activity activity in capturedActivities) {
            activity.TraceId.ShouldBe(traceId, $"Activity '{activity.OperationName}' should share the same trace ID");
        }
    }

    #endregion

    #region Task 4.3: DomainServiceFailure_AllActivitiesHaveCorrelationIdTag

    [Fact]
    public async Task DomainServiceFailure_AllActivitiesHaveCorrelationIdTag() {
        // Arrange
        string correlationId = $"trace-corr-{Guid.NewGuid()}";
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

        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, _) = CreateActorWithFakeDeadLetter();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Every captured activity has the correct correlation ID tag
        capturedActivities.Count.ShouldBeGreaterThan(0);
        foreach (Activity activity in capturedActivities) {
            activity.GetTagItem(EventStoreActivitySource.TagCorrelationId)
                .ShouldBe(correlationId, $"Activity '{activity.OperationName}' should have correlation ID tag");
        }
    }

    #endregion

    #region Task 4.4: DomainServiceFailure_FailingActivityHasErrorStatus

    [Fact]
    public async Task DomainServiceFailure_FailingActivityHasErrorStatus() {
        // Arrange
        string correlationId = $"trace-err-{Guid.NewGuid()}";
        Activity? domainInvokeActivity = null;
        Activity? processActivity = null;

        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId)) {
                    if (activity.OperationName == EventStoreActivitySource.DomainServiceInvoke) {
                        domainInvokeActivity = activity;
                    }
                    else if (activity.OperationName == EventStoreActivitySource.ProcessCommand) {
                        processActivity = activity;
                    }
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, _) = CreateActorWithFakeDeadLetter();
        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Domain service infra failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: The DomainServiceInvoke activity has Error status
        _ = domainInvokeActivity.ShouldNotBeNull("DomainServiceInvoke activity should be captured");
        domainInvokeActivity.Status.ShouldBe(ActivityStatusCode.Error);

        // Assert: The ProcessCommand activity also has Error status
        _ = processActivity.ShouldNotBeNull("ProcessCommand activity should be captured");
        processActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    #endregion

    #region Task 4.5: DomainServiceFailure_DeadLetterActivityRecordsSuccess

    [Fact]
    public async Task DomainServiceFailure_DeadLetterActivityRecordsSuccess() {
        // Arrange: Test DeadLetterPublisher directly (not via actor, since FakeDeadLetterPublisher
        // doesn't create OTel activities)
        string correlationId = $"trace-dl-ok-{Guid.NewGuid()}";
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
        ILogger<DeadLetterPublisher> dlLogger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, dlLogger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        CommandEnvelope commandEnvelope = CreateTestEnvelope(correlationId: correlationId);
        var message = DeadLetterMessage.FromException(
            commandEnvelope, CommandStatus.Processing,
            new InvalidOperationException("Domain service failure"));

        // Act
        bool result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeTrue();
        _ = capturedActivity.ShouldNotBeNull("DeadLetter activity should be created");
        capturedActivity.OperationName.ShouldBe(EventStoreActivitySource.EventsPublishDeadLetter);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
        capturedActivity.Kind.ShouldBe(ActivityKind.Producer);
    }

    #endregion

    #region Task 4.6: DeadLetterPublishFails_DeadLetterActivityRecordsError

    [Fact]
    public async Task DeadLetterPublishFails_DeadLetterActivityRecordsError() {
        // Arrange: Test DeadLetterPublisher when DAPR publish throws
        string correlationId = $"trace-dl-err-{Guid.NewGuid()}";
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
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DeadLetterMessage>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DAPR sidecar unavailable"));

        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions { PubSubName = "pubsub" });
        ILogger<DeadLetterPublisher> dlLogger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, dlLogger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        CommandEnvelope commandEnvelope = CreateTestEnvelope(correlationId: correlationId);
        var message = DeadLetterMessage.FromException(
            commandEnvelope, CommandStatus.Processing,
            new InvalidOperationException("Domain service failure"));

        // Act
        bool result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeFalse();
        _ = capturedActivity.ShouldNotBeNull("DeadLetter activity should still be created on failure");
        capturedActivity.OperationName.ShouldBe(EventStoreActivitySource.EventsPublishDeadLetter);
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Error);
    }

    #endregion

    #region Task 4.7: TraceContext_PropagatesThroughActorProxy_SingleTraceId

    [Fact]
    public async Task TraceContext_PropagatesThroughActorProxy_SingleTraceId() {
        // Arrange: Verify traceparent fallback via CommandEnvelope.Extensions
        // When Activity.Current is null (as in actor proxy crossing), the actor uses
        // traceparent from Extensions to establish parent context.
        string correlationId = $"trace-proxy-{Guid.NewGuid()}";
        var traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        string traceParent = $"00-{traceId.ToHexString()}-{parentSpanId.ToHexString()}-01";

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

        (AggregateActor actor, _, _, IDomainServiceInvoker invoker, _) = CreateActorWithFakeDeadLetter();

        // Command with traceparent in Extensions (fallback mechanism)
        CommandEnvelope envelope = CreateTestEnvelope(
            correlationId: correlationId,
            extensions: new Dictionary<string, string> { ["traceparent"] = traceParent });

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failure after proxy crossing"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: ProcessCommand activity uses the traceparent from Extensions
        _ = processActivity.ShouldNotBeNull("ProcessCommand activity should be created");
        processActivity.TraceId.ShouldBe(traceId, "Trace ID should match the fallback traceparent");
        processActivity.ParentSpanId.ShouldBe(parentSpanId, "Parent span ID should match the fallback traceparent");
    }

    #endregion

    #region Task 4.8: SidecarUnavailable_DeadLetterFailure_ErrorLogHasFullCorrelationContext

    [Fact]
    public async Task SidecarUnavailable_DeadLetterFailure_ErrorLogHasFullCorrelationContext() {
        // Arrange: Dead-letter publication returns false (sidecar unavailable)
        string correlationId = $"trace-sidecar-{Guid.NewGuid()}";

        (AggregateActor actor, _, ILogger<AggregateActor> logger, IDomainServiceInvoker invoker, IDeadLetterPublisher mockDeadLetter) =
            CreateActorWithMockDeadLetter();

        // Simulate dead-letter publication failure
        _ = mockDeadLetter.PublishDeadLetterAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(false);

        CommandEnvelope envelope = CreateTestEnvelope(correlationId: correlationId);

        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Domain service failure"));

        // Act
        _ = await actor.ProcessCommandAsync(envelope);

        // Assert: Error log for DL publication failure contains correlation context
        IReadOnlyList<(LogLevel Level, string Message)> logEntries = GetLogEntries(logger);

        // Should have the InfrastructureFailure Error log
        logEntries.Any(e => e.Level == LogLevel.Error
                 && e.Message.Contains(correlationId, StringComparison.Ordinal)
                 && e.Message.Contains("InfrastructureFailure", StringComparison.Ordinal))
            .ShouldBeTrue("InfrastructureFailure Error log should contain correlation ID");

        // Should have the dead-letter publication failed Error log
        logEntries.Any(e => e.Level == LogLevel.Error
                 && e.Message.Contains(correlationId, StringComparison.Ordinal)
                 && e.Message.Contains("Dead-letter publication failed", StringComparison.Ordinal))
            .ShouldBeTrue("Dead-letter publication failure Error log should contain correlation ID");

        // Verify the DL failure log also contains tenant and domain context
        IReadOnlyList<(LogLevel Level, string Message)> dlFailureLogs = logEntries
            .Where(e => e.Level == LogLevel.Error
                   && e.Message.Contains("Dead-letter publication failed", StringComparison.Ordinal))
            .ToList();

        dlFailureLogs.ShouldNotBeEmpty();
        dlFailureLogs[0].Message.Contains("test-tenant", StringComparison.Ordinal)
            .ShouldBeTrue("DL failure log should contain TenantId for operator diagnosis");
        dlFailureLogs[0].Message.Contains("test-domain", StringComparison.Ordinal)
            .ShouldBeTrue("DL failure log should contain Domain for operator diagnosis");
    }

    #endregion
}
