using System.Diagnostics;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Diagnostics;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public class ProtectedDataDiagnosticRedactionTests : IDisposable {
    private static readonly AggregateIdentity TestIdentity = new("tenant-a", "billing", "agg-001");
    private readonly List<Activity> _activities = [];
    private readonly ActivityListener _activityListener;

    public ProtectedDataDiagnosticRedactionTests() {
        _activityListener = new ActivityListener {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _activities.Add(activity),
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose() {
        _activityListener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LoggingBehavior_ProtectedDataPath_RedactsExceptionMessageInRenderedAndStructuredLogsAndActivity() {
        var logs = new List<CapturedLogEntry>();
        var logger = new CapturingLogger<LoggingBehavior<SubmitCommand, SubmitCommandResult>>(logs);
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = "corr-redact-logging";
        accessor.HttpContext.Returns(httpContext);
        var behavior = new LoggingBehavior<SubmitCommand, SubmitCommandResult>(logger, accessor);
        SubmitCommand command = CreateCommand("corr-redact-logging");

        string unsafeMessage = $"provider failed {ProtectedDataLeakSentinel.ProtectedProviderExceptionText} {ProtectedDataLeakSentinel.ProtectedKeyAlias}";

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                command,
                new(_ => throw new InvalidOperationException(unsafeMessage)),
                CancellationToken.None));

        CapturedLogEntry error = logs.Single(e => e.Level == LogLevel.Error);
        ProtectedDataLeakSentinel.AssertNoLeak([error.Message, error.ExceptionText, .. error.Properties.Select(static p => p.Value?.ToString())]);
        error.Message.ShouldContain("Protected data diagnostic details were redacted.");
        error.Properties.Any(p => p.Key == "SafeDiagnostic"
            && p.Value is not null
            && p.Value.ToString()!.Contains("ReasonCode=", StringComparison.Ordinal)).ShouldBeTrue();

        Activity activity = _activities.Single(a => a.OperationName == "EventStore.Submit"
            && Equals(a.GetTagItem("eventstore.correlation_id"), "corr-redact-logging"));
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        ProtectedDataLeakSentinel.AssertNoLeak(CaptureActivityDiagnostics(activity));
        activity.StatusDescription.ShouldNotBeNull();
        activity.StatusDescription!.ShouldContain("ReasonCode=");
    }

    [Fact]
    public void DeadLetterMessage_FromProtectedDataException_StoresSafeDiagnosticText() {
        CommandEnvelope command = CreateEnvelope();
        var ex = new InvalidOperationException($"provider failed {ProtectedDataLeakSentinel.ProtectedProviderExceptionText}");

        DeadLetterMessage message = DeadLetterMessage.FromException(command, CommandStatus.Rejected, ex);

        message.ErrorMessage.ShouldContain("Protected data diagnostic details were redacted.");
        ProtectedDataLeakSentinel.AssertNoLeak([message.ErrorMessage]);
    }

    [Fact]
    public async Task DeadLetterPublisher_RedactsErrorMessageLogFieldAndPublicationFailureStatus() {
        var logs = new List<CapturedLogEntry>();
        var logger = new CapturingLogger<DeadLetterPublisher>(logs);
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.PublishEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DeadLetterMessage>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException($"dapr failed {ProtectedDataLeakSentinel.ProtectedConnectionString}"));
        var publisher = new DeadLetterPublisher(daprClient, Options.Create(new EventPublisherOptions { PubSubName = "pubsub" }), logger);
        DeadLetterMessage message = DeadLetterMessage.FromException(
            CreateEnvelope(),
            CommandStatus.Rejected,
            new InvalidOperationException($"provider failed {ProtectedDataLeakSentinel.ProtectedProviderExceptionText}"));

        bool published = await publisher.PublishDeadLetterAsync(TestIdentity, message);

        published.ShouldBeFalse();
        CapturedLogEntry failed = logs.Single(e => e.EventId.Id == 3201);
        ProtectedDataLeakSentinel.AssertNoLeak([failed.Message, failed.ExceptionText, .. failed.Properties.Select(static p => p.Value?.ToString())]);
        Activity activity = _activities.Single(a => a.OperationName == "EventStore.Events.PublishDeadLetter"
            && Equals(a.GetTagItem("eventstore.correlation_id"), message.CorrelationId));
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        ProtectedDataLeakSentinel.AssertNoLeak(CaptureActivityDiagnostics(activity));
    }

    [Fact]
    public async Task EventPublisher_GenericPublishException_ReturnsSafeFailureReasonAndActivityStatus() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        daprClient.PublishEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<EventEnvelope>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException($"pubsub failed {ProtectedDataLeakSentinel.ProtectedConnectionString}"));
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        var logs = new List<CapturedLogEntry>();
        var publisher = new EventPublisher(
            daprClient,
            Options.Create(new EventPublisherOptions { PubSubName = "pubsub" }),
            new CapturingLogger<EventPublisher>(logs),
            new NoOpEventPayloadProtectionService(),
            new NoOpProjectionUpdateOrchestrator(),
            hostEnvironment: env);

        EventPublishResult result = await publisher.PublishEventsAsync(
            TestIdentity,
            [CreateEventEnvelope()],
            "corr-redact-publish");

        result.Success.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason!.ShouldContain("Protected data diagnostic details were redacted.");
        ProtectedDataLeakSentinel.AssertNoLeak([result.FailureReason]);
        Activity activity = _activities.Single(a => a.OperationName == "EventStore.Events.Publish"
            && Equals(a.GetTagItem("eventstore.correlation_id"), "corr-redact-publish"));
        CapturedLogEntry failed = logs.Single(e => e.Level == LogLevel.Error);
        ProtectedDataLeakSentinel.AssertNoLeak([failed.Message, failed.ExceptionText, .. failed.Properties.Select(static p => p.Value?.ToString())]);
        ProtectedDataLeakSentinel.AssertNoLeak(CaptureActivityDiagnostics(activity));
    }

    [Fact]
    public void ProtectedDataDiagnosticRedactor_RejectsTokenShapedReasonAndStageValues() {
        string text = ProtectedDataDiagnosticRedactor.BuildSafeText(
            ProtectedDataLeakSentinel.ProtectedKeyAlias,
            ProtectedDataLeakSentinel.ProtectedStateStoreKey);

        text.ShouldBe("Protected data diagnostic details were redacted. ReasonCode=protected-data-diagnostic-redacted; Stage=unspecified.");
        ProtectedDataLeakSentinel.AssertNoLeak([text]);
    }

    [Fact]
    public async Task DeadLetterPublisher_NormalizesExternallySuppliedUnsafeErrorMessageBeforePublishAndLog() {
        var logs = new List<CapturedLogEntry>();
        var logger = new CapturingLogger<DeadLetterPublisher>(logs);
        DaprClient daprClient = Substitute.For<DaprClient>();
        DeadLetterMessage? publishedMessage = null;
        Dictionary<string, string>? publishedMetadata = null;
        daprClient.PublishEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DeadLetterMessage>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                publishedMessage = callInfo.ArgAt<DeadLetterMessage>(2);
                publishedMetadata = callInfo.ArgAt<Dictionary<string, string>>(3);
                return Task.CompletedTask;
            });
        var publisher = new DeadLetterPublisher(daprClient, Options.Create(new EventPublisherOptions { PubSubName = "pubsub" }), logger);
        DeadLetterMessage unsafeMessage = DeadLetterMessage.FromException(
            CreateEnvelope(),
            CommandStatus.Rejected,
            new InvalidOperationException("safe seed")) with {
            ErrorMessage = $"provider leaked {ProtectedDataLeakSentinel.ProtectedProviderExceptionText}",
        };

        bool published = await publisher.PublishDeadLetterAsync(TestIdentity, unsafeMessage);

        published.ShouldBeTrue();
        publishedMessage.ShouldNotBeNull();
        publishedMessage.ErrorMessage.ShouldContain("Protected data diagnostic details were redacted.");
        ProtectedDataLeakSentinel.AssertNoLeak([
            publishedMessage.ErrorMessage,
            .. publishedMetadata!.Select(static p => p.Value),
            .. logs.Select(static e => e.Message),
            .. logs.Select(static e => e.ExceptionText),
            .. logs.SelectMany(static e => e.Properties.Select(static p => p.Value?.ToString())),
        ]);
    }

    [Fact]
    public void ProtectedDataDiagnosticRedactor_BuildsSafeUnreadableProblemExtensionsWithCorrelationId() {
        ProtectedDataReadabilityDecision decision = ProtectedDataReadabilityDecision.FromUnreadable(
            UnreadableProtectedDataReason.MissingKey,
            ProtectedDataDecisionStage.Replay,
            TestIdentity.TenantId,
            TestIdentity.Domain,
            TestIdentity.AggregateId,
            42,
            1,
            "corr-problem");

        IReadOnlyDictionary<string, object?> extensions = ProtectedDataDiagnosticRedactor.BuildUnreadableProblemExtensions(decision);
        string json = JsonSerializer.Serialize(extensions, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        extensions["correlationId"].ShouldBe("corr-problem");
        extensions["reasonCode"].ShouldBe(UnreadableProtectedDataReasonCodes.MissingKey);
        extensions["stage"].ShouldBe("replay");
        ProtectedDataLeakSentinel.AssertNoLeak([json, .. extensions.Values.Select(static v => v?.ToString())]);
    }

    private static SubmitCommand CreateCommand(string correlationId) => new(
        MessageId: "msg-1",
        Tenant: TestIdentity.TenantId,
        Domain: TestIdentity.Domain,
        AggregateId: TestIdentity.AggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId,
        UserId: "user-1",
        Extensions: null);

    private static CommandEnvelope CreateEnvelope() => new(
        MessageId: "msg-1",
        TenantId: TestIdentity.TenantId,
        Domain: TestIdentity.Domain,
        AggregateId: TestIdentity.AggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: "corr-1",
        CausationId: "cause-1",
        UserId: "user-1",
        Extensions: null);

    private static EventEnvelope CreateEventEnvelope() => new(
        MessageId: "msg-1",
        AggregateId: TestIdentity.AggregateId,
        AggregateType: "test-aggregate",
        TenantId: TestIdentity.TenantId,
        Domain: TestIdentity.Domain,
        SequenceNumber: 1,
        GlobalPosition: 1,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: "corr-redact-publish",
        CausationId: "cause-1",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "OrderCreated",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: [1, 2, 3],
        Extensions: EventStorePayloadProtectionMetadataCarrier.Write(
            (IDictionary<string, string>?)null,
            EventStorePayloadProtectionMetadata.Unprotected()));

    private sealed class CapturingLogger<T>(List<CapturedLogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            IReadOnlyList<KeyValuePair<string, object?>> properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? [.. pairs]
                : [];
            entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), exception?.ToString(), properties));
        }
    }

    private static IEnumerable<string?> CaptureActivityDiagnostics(Activity activity) {
        yield return activity.StatusDescription;
        foreach (KeyValuePair<string, object?> tag in activity.TagObjects) {
            yield return tag.Value?.ToString();
        }

        foreach (ActivityEvent activityEvent in activity.Events) {
            yield return activityEvent.Name;
            foreach (KeyValuePair<string, object?> tag in activityEvent.Tags) {
                yield return tag.Value?.ToString();
            }
        }
    }

    private sealed record CapturedLogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        string? ExceptionText,
        IReadOnlyList<KeyValuePair<string, object?>> Properties);
}
