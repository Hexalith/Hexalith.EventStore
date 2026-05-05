using System.Net;
using System.Text;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

// ATDD red-phase scaffolds for story:
//   _bmad-output/implementation-artifacts/post-epic-deferred-dw1-projection-and-drain-hardening.md
// Tests are marked [Fact(Skip = "...")] so CI stays green during staged implementation.
// Dev removes Skip per AC as the corresponding production code lands.
//
// Stable reason codes asserted here (must match the diagnostic vocabulary in the story):
//   project_upstream_4xx, project_upstream_5xx,
//   project_unsupported_content_type, project_invalid_charset, project_malformed_json,
//   project_invalid_projection_type, project_invalid_state,
//   project_timeout, project_cancelled, checkpoint_drift, unknown.
public class Dw1ProjectionDeliveryAtddTests
{
    private const string SkipReasonAc1 = "ATDD red phase — DW1 AC#1 (checkpoint drift). Remove Skip when implementing.";
    private const string SkipReasonAc2 = "ATDD red phase — DW1 AC#2 (/project failure classification). Remove Skip when implementing.";
    private const string SkipReasonAc3 = "ATDD red phase — DW1 AC#3 (cancellation vs timeout). Remove Skip when implementing.";
    private const string SkipReasonAc4 = "ATDD red phase — DW1 AC#4 (per-aggregate serialization). Remove Skip when implementing.";

    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");
    private static readonly DomainServiceRegistration TestRegistration = new(
        "counter-service", "project", "test-tenant", "test-domain", "v1");

    private static EventEnvelope CreateEnvelope(long sequenceNumber) =>
        new(
            MessageId: $"msg-{sequenceNumber}",
            AggregateId: "agg-001",
            AggregateType: "test-aggregate",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            GlobalPosition: sequenceNumber * 10,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: $"cause-{sequenceNumber}",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "CounterIncremented",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static (
        ProjectionUpdateOrchestrator Sut,
        IActorProxyFactory ActorProxyFactory,
        IDomainServiceResolver Resolver,
        IProjectionCheckpointTracker CheckpointTracker,
        IProjectionWriteActor WriteActor,
        IAggregateActor AggregateActor,
        List<LogEntry> Logs)
        CreateSut(
            HttpMessageHandler responseHandler,
            long? checkpointSequence = null,
            EventEnvelope[]? events = null)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        IProjectionCheckpointTracker checkpointTracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = checkpointTracker.ReadLastDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<CancellationToken>())
            .Returns(checkpointSequence ?? 0);
        _ = checkpointTracker.SaveDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(events ?? [CreateEnvelope(1)]);
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(writeActor);

        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TestRegistration);

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        var httpClient = new HttpClient(responseHandler);
        _ = httpClientFactory.CreateClient().Returns(httpClient);
        _ = httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        DaprClient daprClient = new DaprClientBuilder().Build();
        IOptions<ProjectionOptions> options = Options.Create(new ProjectionOptions());
        var logs = new List<LogEntry>();
        var logger = new TestLogger<ProjectionUpdateOrchestrator>(logs);
        var sut = new ProjectionUpdateOrchestrator(
            actorProxyFactory, daprClient, httpClientFactory, resolver, checkpointTracker, options, logger);

        return (sut, actorProxyFactory, resolver, checkpointTracker, writeActor, aggregateActor, logs);
    }

    // ---------- AC #1: Checkpoint drift ----------

    [Fact(Skip = SkipReasonAc1)]
    public async Task DeliverProjection_PersistedCheckpointAheadOfAggregateStream_EmitsCheckpointDriftReasonCode()
    {
        // Arrange — checkpoint claims sequence 99 was delivered, but the aggregate
        // actor only has events up to sequence 5. This is impossible state and must
        // be detected and reported with the stable reason code.
        var driftHandler = new JsonStringHandler("""{"projectionType":"counter-summary","state":{"value":1}}""");
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, IProjectionWriteActor writeActor, _, List<LogEntry> logs) =
            CreateSut(driftHandler, checkpointSequence: 99, events: [CreateEnvelope(5)]);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert — stable reason code emitted, write actor not called, checkpoint NOT advanced.
        logs.ShouldContain(e => e.Message.Contains("checkpoint_drift"), customMessage: "checkpoint_drift reason code missing from log entries");
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await tracker.DidNotReceive().SaveDeliveredSequenceAsync(TestIdentity, Arg.Is<long>(s => s < 99), Arg.Any<CancellationToken>());
    }

    [Fact(Skip = SkipReasonAc1)]
    public async Task DeliverProjection_PersistedCheckpointAheadOfAggregateStream_DoesNotRegressCheckpoint()
    {
        // Drift must never cause the checkpoint to be saved at a lower value.
        var handler = new JsonStringHandler("""{"projectionType":"counter-summary","state":{"value":1}}""");
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, _, _, _) =
            CreateSut(handler, checkpointSequence: 99, events: [CreateEnvelope(5)]);

        await sut.UpdateProjectionAsync(TestIdentity);

        await tracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    // ---------- AC #2: /project failure classification ----------

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_Upstream4xx_LogsProjectUpstream4xxReasonCode()
    {
        var handler = new StatusCodeOnlyHandler(HttpStatusCode.BadRequest);
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, IProjectionWriteActor writeActor, _, List<LogEntry> logs) =
            CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_upstream_4xx"));
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await tracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_Upstream5xx_LogsProjectUpstream5xxReasonCode()
    {
        var handler = new StatusCodeOnlyHandler(HttpStatusCode.BadGateway);
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, IProjectionWriteActor writeActor, _, List<LogEntry> logs) =
            CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_upstream_5xx"));
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await tracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_UnsupportedContentType_LogsProjectUnsupportedContentTypeReasonCode()
    {
        var handler = new ContentTypeHandler(@"<xml/>", mediaType: "application/xml");
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, IProjectionWriteActor writeActor, _, List<LogEntry> logs) =
            CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_unsupported_content_type"));
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await tracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_InvalidCharset_LogsProjectInvalidCharsetReasonCode()
    {
        var handler = new InvalidCharsetHandler();
        (ProjectionUpdateOrchestrator sut, _, _, _, _, _, List<LogEntry> logs) = CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_invalid_charset"));
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_MalformedJson_LogsProjectMalformedJsonReasonCode()
    {
        var handler = new JsonStringHandler("{not-json}");
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, IProjectionWriteActor writeActor, _, List<LogEntry> logs) =
            CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_malformed_json"));
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await tracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_EmptyProjectionType_LogsProjectInvalidProjectionTypeReasonCode()
    {
        // Today this case logs "ProjectionType is null or empty." (free-form text).
        // After DW1 it must log the stable reason code project_invalid_projection_type.
        var handler = new JsonStringHandler("""{"projectionType":"","state":{"value":1}}""");
        (ProjectionUpdateOrchestrator sut, _, _, _, _, _, List<LogEntry> logs) = CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_invalid_projection_type"));
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_NullState_LogsProjectInvalidStateReasonCode()
    {
        // Today this case logs "State is null or undefined." After DW1 it must log
        // the stable reason code project_invalid_state.
        var handler = new JsonStringHandler("""{"projectionType":"counter-summary","state":null}""");
        (ProjectionUpdateOrchestrator sut, _, _, _, _, _, List<LogEntry> logs) = CreateSut(handler);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldContain(e => e.Message.Contains("project_invalid_state"));
    }

    [Fact(Skip = SkipReasonAc2)]
    public async Task DeliverProjection_AnyClassifiedFailure_DoesNotLogEventPayload()
    {
        // Privacy invariant: failure logs must never carry event payload bytes.
        var handler = new StatusCodeOnlyHandler(HttpStatusCode.InternalServerError);
        (ProjectionUpdateOrchestrator sut, _, _, _, _, _, List<LogEntry> logs) =
            CreateSut(handler, events: [CreateEnvelope(1) with { Payload = Encoding.UTF8.GetBytes("SECRET-payload-marker") }]);

        await sut.UpdateProjectionAsync(TestIdentity);

        logs.ShouldNotContain(e => e.Message.Contains("SECRET-payload-marker"));
    }

    // ---------- AC #3: Cancellation vs timeout ----------

    [Fact(Skip = SkipReasonAc3)]
    public async Task DeliverProjection_HostTokenCancelled_PropagatesOperationCanceledException()
    {
        var handler = new JsonStringHandler("""{"projectionType":"counter-summary","state":{"value":1}}""");
        (ProjectionUpdateOrchestrator sut, _, _, _, _, _, _) = CreateSut(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => sut.UpdateProjectionAsync(TestIdentity, cts.Token));
    }

    [Fact(Skip = SkipReasonAc3)]
    public async Task DeliverProjection_HttpTimeoutWhileHostStillRunning_LogsProjectTimeoutAndDoesNotThrow()
    {
        // Inner HTTP timeout (e.g. linked-CTS timeout inside Dapr service invocation)
        // must NOT escape as host shutdown. It is a transient projection failure with
        // a stable reason code.
        var handler = new TaskCanceledTimeoutHandler();
        (ProjectionUpdateOrchestrator sut, _, _, IProjectionCheckpointTracker tracker, IProjectionWriteActor writeActor, _, List<LogEntry> logs) =
            CreateSut(handler);

        // Host token NOT cancelled — only inner HTTP times out.
        await Should.NotThrowAsync(() => sut.UpdateProjectionAsync(TestIdentity, CancellationToken.None));

        logs.ShouldContain(e => e.Message.Contains("project_timeout"));
        await writeActor.DidNotReceiveWithAnyArgs().UpdateProjectionAsync(default!);
        await tracker.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default, default);
    }

    // ---------- AC #4: Per-aggregate serialization ----------

    [Fact(Skip = SkipReasonAc4)]
    public async Task DeliverProjection_TwoOverlappingCallsForSameActorId_AreSerializedByKeyedSemaphore()
    {
        // Two parallel deliveries on the same identity must not enter the inner section concurrently.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int concurrent = 0;
        int maxObservedConcurrent = 0;

        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        IProjectionCheckpointTracker tracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = tracker.SaveDeliveredSequenceAsync(Arg.Any<AggregateIdentity>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        async Task<EventEnvelope[]> GatedGetEventsAsync()
        {
            int observed = Interlocked.Increment(ref concurrent);
            int previousMax;
            do
            {
                previousMax = maxObservedConcurrent;
                if (observed <= previousMax)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(ref maxObservedConcurrent, observed, previousMax) != previousMax);

            await gate.Task.ConfigureAwait(false);
            _ = Interlocked.Decrement(ref concurrent);
            return [CreateEnvelope(1)];
        }

        _ = aggregateActor.GetEventsAsync(0).Returns(_ => GatedGetEventsAsync());
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(aggregateActor);
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<ActorId>(), QueryRouter.ProjectionActorTypeName)
            .Returns(Substitute.For<IProjectionWriteActor>());
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TestRegistration);

        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        var client = new HttpClient(new JsonStringHandler("""{"projectionType":"counter-summary","state":{"value":1}}"""));
        _ = httpClientFactory.CreateClient().Returns(client);

        DaprClient daprClient = new DaprClientBuilder().Build();
        var sut = new ProjectionUpdateOrchestrator(
            actorProxyFactory, daprClient, httpClientFactory, resolver, tracker,
            Options.Create(new ProjectionOptions()),
            new TestLogger<ProjectionUpdateOrchestrator>([]));

        Task first = sut.UpdateProjectionAsync(TestIdentity);
        Task second = sut.UpdateProjectionAsync(TestIdentity);

        // Give both tasks time to enter the lock contention.
        await Task.Delay(50);

        // Only one should be inside GetEventsAsync at a time.
        Volatile.Read(ref maxObservedConcurrent).ShouldBe(1);

        gate.SetResult(true);
        await Task.WhenAll(first, second);
    }

    // ---------- HTTP handlers ----------

    private sealed class JsonStringHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class StatusCodeOnlyHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
    }

    private sealed class ContentTypeHandler(string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            });
    }

    private sealed class InvalidCharsetHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent("{}"u8.ToArray());
            content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=not-a-real-charset");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class TaskCanceledTimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Simulates an inner HTTP timeout: TaskCanceledException with no host-token cancellation.
            using var innerTimeoutCts = new CancellationTokenSource();
            innerTimeoutCts.Cancel();
            throw new TaskCanceledException("Simulated HTTP timeout (inner CTS, host still running).", null, innerTimeoutCts.Token);
        }
    }
}
