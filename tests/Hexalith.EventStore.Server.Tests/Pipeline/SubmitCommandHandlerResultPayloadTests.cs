using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Pipeline;

public class SubmitCommandHandlerResultPayloadTests {
    private const int ResultPayloadDroppedEventId = 1107;
    private const int StatusReadForTrackingFailedEventId = 1106;
    private const string ResultPayloadDroppedStage = "ResultPayloadDropped";
    private const string SensitiveResultPayload = "{\"value\":\"RESULT-PAYLOAD-SENTINEL-DO-NOT-LOG\"}";

    [Fact]
    public async Task Handle_CompletedFinalStatus_ReturnsResultPayloadAndDoesNotWarn() {
        // Arrange
        var logs = new List<CapturedLogEntry>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var router = CreateRouter(SensitiveResultPayload);
        SubmitCommand command = CreateCommand();

        _ = statusStore.ReadStatusAsync(command.Tenant, command.CorrelationId, Arg.Any<CancellationToken>())
            .Returns(CreateStatus(CommandStatus.Completed, command.AggregateId));

        var handler = CreateHandler(statusStore, router, logs);

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(command.CorrelationId);
        result.ResultPayload.ShouldBe(SensitiveResultPayload);
        logs.ShouldNotContain(static entry => entry.EventId.Id == ResultPayloadDroppedEventId);
    }

    [Fact]
    public async Task Handle_NonCompletedFinalStatus_DropsPayloadAndLogsAllowedMetadataOnce() {
        // Arrange
        var logs = new List<CapturedLogEntry>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var router = CreateRouter(SensitiveResultPayload);
        SubmitCommand command = CreateCommand();

        _ = statusStore.ReadStatusAsync(command.Tenant, command.CorrelationId, Arg.Any<CancellationToken>())
            .Returns(CreateStatus(CommandStatus.PublishFailed, command.AggregateId));

        var handler = CreateHandler(statusStore, router, logs);

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(command.CorrelationId);
        result.ResultPayload.ShouldBeNull();

        CapturedLogEntry warning = logs.Single(entry => entry.EventId.Id == ResultPayloadDroppedEventId);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(ResultPayloadDroppedStage);
        warning.ExceptionText.ShouldBeNull();
        AssertAllowedDropProperties(warning, command, "PublishFailed", statusReadSucceeded: true);
        AssertSensitiveResultPayloadNotLogged(logs);
    }

    [Fact]
    public async Task Handle_StatusReadFailure_DropsPayloadAndLogsReadFailureAndDropWarning() {
        // Arrange
        var logs = new List<CapturedLogEntry>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var router = CreateRouter(SensitiveResultPayload);
        SubmitCommand command = CreateCommand();

        _ = statusStore.ReadStatusAsync(command.Tenant, command.CorrelationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Status store unavailable"));

        var handler = CreateHandler(statusStore, router, logs);

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(command.CorrelationId);
        result.ResultPayload.ShouldBeNull();

        logs.Count(entry => entry.EventId.Id == StatusReadForTrackingFailedEventId).ShouldBe(1);
        CapturedLogEntry warning = logs.Single(entry => entry.EventId.Id == ResultPayloadDroppedEventId);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(ResultPayloadDroppedStage);
        warning.ExceptionText.ShouldBeNull();
        AssertAllowedDropProperties(warning, command, "Unavailable", statusReadSucceeded: false);
        AssertSensitiveResultPayloadNotLogged(logs);
    }

    [Fact]
    public async Task Handle_MissingFinalStatus_DropsPayloadAndLogsSuccessfulReadWithUnavailableStatus() {
        // Arrange
        var logs = new List<CapturedLogEntry>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var router = CreateRouter(SensitiveResultPayload);
        SubmitCommand command = CreateCommand();

        _ = statusStore.ReadStatusAsync(command.Tenant, command.CorrelationId, Arg.Any<CancellationToken>())
            .Returns((CommandStatusRecord?)null);

        var handler = CreateHandler(statusStore, router, logs);

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(command.CorrelationId);
        result.ResultPayload.ShouldBeNull();

        logs.ShouldNotContain(static entry => entry.EventId.Id == StatusReadForTrackingFailedEventId);
        CapturedLogEntry warning = logs.Single(entry => entry.EventId.Id == ResultPayloadDroppedEventId);
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain(ResultPayloadDroppedStage);
        warning.ExceptionText.ShouldBeNull();
        AssertAllowedDropProperties(warning, command, "Unavailable", statusReadSucceeded: true);
        AssertSensitiveResultPayloadNotLogged(logs);
    }

    [Fact]
    public async Task Handle_NullActorPayload_DoesNotReadStatusOnlyForPayloadAndDoesNotWarn() {
        // Arrange
        var logs = new List<CapturedLogEntry>();
        var statusStore = Substitute.For<ICommandStatusStore>();
        var router = CreateRouter(resultPayload: null);
        SubmitCommand command = CreateCommand();
        var handler = CreateHandler(statusStore, router, logs);

        // Act
        SubmitCommandResult result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.CorrelationId.ShouldBe(command.CorrelationId);
        result.ResultPayload.ShouldBeNull();
        logs.ShouldNotContain(static entry => entry.EventId.Id == ResultPayloadDroppedEventId);
        _ = await statusStore.DidNotReceive().ReadStatusAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static SubmitCommandHandler CreateHandler(
        ICommandStatusStore statusStore,
        ICommandRouter router,
        List<CapturedLogEntry> logs) =>
        new(statusStore, new InMemoryCommandArchiveStore(), router, new CapturingLogger<SubmitCommandHandler>(logs));

    private static ICommandRouter CreateRouter(string? resultPayload) {
        ICommandRouter router = Substitute.For<ICommandRouter>();
        _ = router.RouteCommandAsync(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new CommandProcessingResult(
                Accepted: true,
                CorrelationId: callInfo.Arg<SubmitCommand>().CorrelationId,
                ResultPayload: resultPayload));
        return router;
    }

    private static SubmitCommand CreateCommand() => new(
        MessageId: "msg-result-payload-drop",
        Tenant: "test-tenant",
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: "corr-result-payload-drop",
        UserId: "test-user",
        Extensions: null);

    private static CommandStatusRecord CreateStatus(CommandStatus status, string aggregateId) => new(
        status,
        DateTimeOffset.UtcNow,
        aggregateId,
        EventCount: status == CommandStatus.Completed ? 1 : null,
        RejectionEventType: null,
        FailureReason: null,
        TimeoutDuration: null);

    private static void AssertAllowedDropProperties(
        CapturedLogEntry warning,
        SubmitCommand command,
        string expectedFinalStatus,
        bool statusReadSucceeded) {
        Dictionary<string, object?> properties = warning.Properties
            .Where(static property => property.Key != "{OriginalFormat}")
            .ToDictionary(static property => property.Key, static property => property.Value);

        properties.Keys.Order().ShouldBe([
            "AggregateId",
            "CommandType",
            "CorrelationId",
            "FinalStatus",
            "StatusReadSucceeded",
            "TenantId",
        ]);
        properties["CorrelationId"].ShouldBe(command.CorrelationId);
        properties["TenantId"].ShouldBe(command.Tenant);
        properties["AggregateId"].ShouldBe(command.AggregateId);
        properties["CommandType"].ShouldBe(command.CommandType);
        properties["FinalStatus"].ShouldBe(expectedFinalStatus);
        properties["StatusReadSucceeded"].ShouldBe(statusReadSucceeded);
    }

    private static void AssertSensitiveResultPayloadNotLogged(IEnumerable<CapturedLogEntry> logs) {
        foreach (CapturedLogEntry entry in logs) {
            entry.Message.ShouldNotContain("RESULT-PAYLOAD-SENTINEL-DO-NOT-LOG");
            if (entry.ExceptionText is not null) {
                entry.ExceptionText.ShouldNotContain("RESULT-PAYLOAD-SENTINEL-DO-NOT-LOG");
            }

            foreach (KeyValuePair<string, object?> property in entry.Properties) {
                property.Key.ShouldNotContain("RESULT-PAYLOAD-SENTINEL-DO-NOT-LOG");
                string? propertyValue = property.Value?.ToString();
                if (propertyValue is not null) {
                    propertyValue.ShouldNotContain("RESULT-PAYLOAD-SENTINEL-DO-NOT-LOG");
                }
            }
        }
    }

    private sealed class CapturingLogger<T>(List<CapturedLogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) {
            IReadOnlyList<KeyValuePair<string, object?>> properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? [.. pairs]
                : [];
            entries.Add(new CapturedLogEntry(logLevel, eventId, formatter(state, exception), exception?.ToString(), properties));
        }
    }

    private sealed record CapturedLogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        string? ExceptionText,
        IReadOnlyList<KeyValuePair<string, object?>> Properties);
}
