
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.3 Task 7: EventPublisher no-retry compliance tests.
/// Verifies that EventPublisher has ZERO custom retry logic -- all retries are DAPR resiliency policies (rule #4).
/// </summary>
public class EventPublisherRetryComplianceTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEnvelope(long sequenceNumber = 1) =>
        new(
            AggregateId: "agg-001",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "OrderCreated",
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static (EventPublisher Publisher, DaprClient DaprClient) CreatePublisher() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions());
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService());
        return (publisher, daprClient);
    }

    // --- Task 7.2: Single call per event on failure ---

    [Fact]
    public async Task PublishEventsAsync_TransientFailure_NoRetry_ReturnsFailed() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Transient broker error"));

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            TestIdentity, [CreateTestEnvelope()], "corr-001");

        // Assert -- exactly 1 call, no retry
        result.Success.ShouldBeFalse();
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    // --- Task 7.3: Each event gets exactly 1 call ---

    [Fact]
    public async Task PublishEventsAsync_SingleCallPerEvent_Verified() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1),
            CreateTestEnvelope(2),
            CreateTestEnvelope(3),
        };

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            TestIdentity, events, "corr-001");

        // Assert -- exactly 3 calls, one per event
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(3);
        await daprClient.Received(3).PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    // --- Task 7.4: No retry library imports ---

    [Fact]
    public void EventPublisher_NoRetryPolicyImported_NoPollyReference() {
        // Arrange -- read the EventPublisher source file
        string sourcePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "Hexalith.EventStore.Server", "Events", "EventPublisher.cs"));
        string sourceCode = File.ReadAllText(sourcePath);

        // Assert -- no retry library usage
        sourceCode.ShouldNotContain("using Polly", customMessage:
            "EventPublisher must not use Polly -- DAPR handles retries (rule #4)");
        sourceCode.ShouldNotContain("RetryPolicy", customMessage:
            "EventPublisher must not define custom retry policies");
        sourceCode.ShouldNotContain("System.Net.Http.Retry", customMessage:
            "EventPublisher must not use System.Net.Http retry");
        sourceCode.ShouldNotContain("while (", customMessage:
            "EventPublisher must not contain retry loops");
        sourceCode.ShouldNotContain("for (int retry", customMessage:
            "EventPublisher must not contain retry counters");
    }

    // --- Task 7.5: Exception caught and returned, not retried ---

    [Fact]
    public async Task PublishEventsAsync_DaprClientException_CaughtNotRetried() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient) = CreatePublisher();
        int callCount = 0;
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                callCount++;
                throw new InvalidOperationException("DaprClient internal error");
            });

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(
            TestIdentity, [CreateTestEnvelope()], "corr-001");

        // Assert
        result.Success.ShouldBeFalse();
        result.FailureReason!.ShouldContain("DaprClient internal error");
        callCount.ShouldBe(1, "EventPublisher should call DaprClient exactly once -- no retry");
    }
}
