using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// A projection-scoped checkpoint advance can be refused for four structurally different reasons, only one of
/// which is an optimistic-concurrency race. Reporting all of them through the orchestrator's single
/// <c>ProjectionCheckpointSaveExhausted</c> warning makes a permanent, structural refusal indistinguishable from a
/// transient write race — and a checkpoint that never advances means the poller re-delivers the same aggregate
/// indefinitely, for every tenant. Each refusal must therefore name itself.
/// </summary>
public sealed class ProjectionCheckpointRefusalDiagnosticsTests {
    private const string ProjectionName = "counter-summary";
    private const string StateStoreName = "statestore";
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    [Fact]
    public async Task PostCutoverWriterProtocolRefusalNamesItselfRatherThanReportingAConcurrencyRace() {
        RecordingLogger logger = new();
        DaprClient daprClient = Substitute.For<DaprClient>();

        // No scoped delivery row exists, and the writer-protocol marker is current. This refusal applies even
        // though the aggregate has nothing to protect, so it silently blocks every legacy advance.
        _ = daprClient.GetStateAsync<ProjectionDeliveryState?>(
                StateStoreName,
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ProjectionDeliveryState?)null);
        _ = daprClient.GetStateAsync<ProjectionDeliveryWriterProtocol>(
                StateStoreName,
                ProjectionDeliveryStateKeys.WriterProtocol,
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryWriterProtocol(
                ProjectionDeliveryWriterProtocol.CurrentSchemaVersion,
                ProjectionDeliveryWriterProtocol.CurrentWriterProtocolVersion,
                "cutover-commit",
                DateTimeOffset.UtcNow));

        ProjectionCheckpointTracker tracker = new(
            daprClient,
            Options.Create(new ProjectionOptions { CheckpointStateStoreName = StateStoreName }),
            logger);

        bool saved = await tracker.SaveDeliveredSequenceAsync(
            TestIdentity,
            ProjectionName,
            7,
            TestContext.Current.CancellationToken);

        saved.ShouldBeFalse();
        logger.Messages.ShouldContain(message =>
            message.Contains("ProjectionCheckpointAdvanceRefused", StringComparison.Ordinal) &&
            message.Contains(
                ProjectionCheckpointTracker.CheckpointRefusalReasonCodes.PostCutoverWriterProtocol,
                StringComparison.Ordinal));
    }

    private sealed class RecordingLogger : ILogger<ProjectionCheckpointTracker> {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) {
            ArgumentNullException.ThrowIfNull(formatter);
            _messages.Add(formatter(state, exception));
        }
    }
}
