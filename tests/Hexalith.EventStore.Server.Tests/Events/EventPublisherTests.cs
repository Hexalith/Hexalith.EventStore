
using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.1 Task 6: EventPublisher unit tests.
/// Verifies CloudEvents metadata, topic derivation, failure handling, OpenTelemetry activity, and structured logging.
/// </summary>
public class EventPublisherTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEnvelope(long sequenceNumber = 1, string eventTypeName = "OrderCreated") =>
        new(
            MessageId: $"msg-{sequenceNumber}",
            AggregateId: "agg-001",
            AggregateType: "test-aggregate",
            TenantId: "test-tenant",
            Domain: "test-domain",
            SequenceNumber: sequenceNumber,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-001",
            CausationId: "cause-001",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: eventTypeName,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2, 3],
            Extensions: null);

    private static IHostEnvironment CreateDevelopmentEnvironment() {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        return env;
    }

    private static (EventPublisher Publisher, DaprClient DaprClient, ILogger<EventPublisher> Logger) CreatePublisher(string pubSubName = "pubsub") {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions { PubSubName = pubSubName });
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), new NoOpProjectionUpdateOrchestrator(), hostEnvironment: CreateDevelopmentEnvironment());
        return (publisher, daprClient, logger);
    }

    private static (EventPublisher Publisher, DaprClient DaprClient, ILogger<EventPublisher> Logger) CreatePublisher(EventPublisherOptions publisherOptions) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(publisherOptions);
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), new NoOpProjectionUpdateOrchestrator(), hostEnvironment: CreateDevelopmentEnvironment());
        return (publisher, daprClient, logger);
    }

    private static (EventPublisher Publisher, DaprClient DaprClient, ILogger<EventPublisher> Logger) CreatePublisher(
        EventPublisherOptions publisherOptions,
        IHostEnvironment hostEnvironment) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IOptions<EventPublisherOptions> options = Options.Create(publisherOptions);
        ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), new NoOpProjectionUpdateOrchestrator(), hostEnvironment: hostEnvironment);
        return (publisher, daprClient, logger);
    }

    // --- Task 6.1: Single event CloudEvents metadata ---

    [Fact]
    public async Task PublishEventsAsync_SingleEvent_CallsDaprPublishWithCloudEventsMetadata() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        EventEnvelope envelope = CreateTestEnvelope();
        IReadOnlyList<EventEnvelope> events = [envelope];

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, events, "corr-001");

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(1);
        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "test-tenant.test-domain.events",
            envelope,
            Arg.Is<Dictionary<string, string>>(m =>
                m["cloudevent.type"] == "OrderCreated" &&
                m["cloudevent.source"] == "hexalith-eventstore/test-tenant/test-domain" &&
                m["cloudevent.id"] == "corr-001:1"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 6.2: Multiple events in sequence ---

    [Fact]
    public async Task PublishEventsAsync_MultipleEvents_PublishesInSequenceOrder() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1, "OrderCreated"),
            CreateTestEnvelope(2, "OrderConfirmed"),
            CreateTestEnvelope(3, "OrderShipped"),
        };
        var callOrder = new List<long>();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci => callOrder.Add(ci.ArgAt<EventEnvelope>(2).SequenceNumber));

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, events, "corr-001");

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(3);
        callOrder.ShouldBe([1, 2, 3]);
    }

    // --- Task 6.3: Topic derivation ---

    [Fact]
    public async Task PublishEventsAsync_CorrectTopic_DerivedFromAggregateIdentity() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        var identity = new AggregateIdentity("acme", "orders", "order-42");
        EventEnvelope envelope = new(
            "msg-1", "order-42", "test-aggregate", "acme", "orders", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json", [1], null);

        // Act
        _ = await publisher.PublishEventsAsync(identity, [envelope], "corr-1");

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            "acme.orders.events",
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 6.4: CloudEvents type ---

    [Fact]
    public async Task PublishEventsAsync_CloudEventsType_MatchesEventTypeName() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        EventEnvelope envelope = CreateTestEnvelope(eventTypeName: "CounterIncremented");

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Is<Dictionary<string, string>>(m => m["cloudevent.type"] == "CounterIncremented"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 6.5: CloudEvents source ---

    [Fact]
    public async Task PublishEventsAsync_CloudEventsSource_IncludesTenantAndDomain() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Is<Dictionary<string, string>>(m => m["cloudevent.source"] == "hexalith-eventstore/test-tenant/test-domain"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 6.6: CloudEvents id ---

    [Fact]
    public async Task PublishEventsAsync_CloudEventsId_CombinesCorrelationIdAndSequence() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        EventEnvelope envelope = CreateTestEnvelope(sequenceNumber: 42);

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-xyz");

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Is<Dictionary<string, string>>(m => m["cloudevent.id"] == "corr-xyz:42"),
            Arg.Any<CancellationToken>());
    }

    // --- Task 6.7: Configurable pub/sub name ---

    [Fact]
    public async Task PublishEventsAsync_UsesConfiguredPubSubName() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(pubSubName: "custom-pubsub");
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            "custom-pubsub",
            Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    // --- Task 6.8: Pub/sub failure ---

    [Fact]
    public async Task PublishEventsAsync_PubSubFailure_ReturnsFailureResult_DoesNotThrow() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Pub/sub component unavailable"));
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-001");

        // Assert -- does NOT throw, returns failure result
        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(0);
        result.FailureReason!.ShouldContain("Pub/sub component unavailable");
    }

    [Fact]
    public async Task PublishEventsAsync_TestPublishFaultFileExists_ReturnsFailureWithoutCallingDapr() {
        string faultFile = Path.Combine(Path.GetTempPath(), $"hexalith-pubsub-fault-{Guid.NewGuid():N}.flag");
        File.WriteAllText(faultFile, "fault");
        try {
            (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(new EventPublisherOptions {
                PubSubName = "pubsub",
                TestPublishFaultFilePath = faultFile,
            });

            EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [CreateTestEnvelope()], "corr-001");

            result.Success.ShouldBeFalse();
            result.PublishedCount.ShouldBe(0);
            result.FailureReason.ShouldNotBeNull();
            result.FailureReason.ShouldContain("Configured test publish fault");
            await daprClient.DidNotReceiveWithAnyArgs().PublishEventAsync<EventEnvelope>(default!, default!, default!, default!, default);
        }
        finally {
            File.Delete(faultFile);
        }
    }

    [Fact]
    public async Task PublishEventsAsync_TestPublishFault_IgnoredInProductionEnvironment() {
        string faultFile = Path.Combine(Path.GetTempPath(), $"hexalith-pubsub-fault-{Guid.NewGuid():N}.flag");
        File.WriteAllText(faultFile, "fault");
        try {
            IHostEnvironment productionEnv = Substitute.For<IHostEnvironment>();
            productionEnv.EnvironmentName.Returns(Environments.Production);

            (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(
                new EventPublisherOptions {
                    PubSubName = "pubsub",
                    TestPublishFaultFilePath = faultFile,
                },
                productionEnv);

            EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [CreateTestEnvelope()], "corr-001");

            // Fault file is present, but Production environment must keep the publisher inert.
            result.Success.ShouldBeTrue();
            await daprClient.Received(1).PublishEventAsync(
                "pubsub",
                Arg.Any<string>(),
                Arg.Any<EventEnvelope>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }
        finally {
            File.Delete(faultFile);
        }
    }

    [Fact]
    public async Task PublishEventsAsync_TestPublishFault_IgnoredWhenHostEnvironmentNull() {
        string faultFile = Path.Combine(Path.GetTempPath(), $"hexalith-pubsub-fault-{Guid.NewGuid():N}.flag");
        File.WriteAllText(faultFile, "fault");
        try {
            DaprClient daprClient = Substitute.For<DaprClient>();
            IOptions<EventPublisherOptions> options = Options.Create(new EventPublisherOptions {
                PubSubName = "pubsub",
                TestPublishFaultFilePath = faultFile,
            });
            ILogger<EventPublisher> logger = Substitute.For<ILogger<EventPublisher>>();
            _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
            // Default ctor (no hostEnvironment) must behave like Production -- fault check disabled.
            var publisher = new EventPublisher(daprClient, options, logger, new NoOpEventPayloadProtectionService(), new NoOpProjectionUpdateOrchestrator());

            EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [CreateTestEnvelope()], "corr-001");

            result.Success.ShouldBeTrue();
            await daprClient.Received(1).PublishEventAsync(
                "pubsub",
                Arg.Any<string>(),
                Arg.Any<EventEnvelope>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }
        finally {
            File.Delete(faultFile);
        }
    }

    [Fact]
    public async Task PublishEventsAsync_TestPublishFaultPrefixDoesNotMatch_PublishesNormally() {
        string faultFile = Path.Combine(Path.GetTempPath(), $"hexalith-pubsub-fault-{Guid.NewGuid():N}.flag");
        File.WriteAllText(faultFile, "fault");
        try {
            (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher(new EventPublisherOptions {
                PubSubName = "pubsub",
                TestPublishFaultFilePath = faultFile,
                TestPublishFaultCorrelationIdPrefix = "r4a5-",
            });

            EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [CreateTestEnvelope()], "corr-001");

            result.Success.ShouldBeTrue();
            await daprClient.Received(1).PublishEventAsync(
                "pubsub",
                "test-tenant.test-domain.events",
                Arg.Any<EventEnvelope>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }
        finally {
            File.Delete(faultFile);
        }
    }

    // --- Task 6.9: Partial failure ---

    [Fact]
    public async Task PublishEventsAsync_PartialFailure_ReturnsPublishedCount() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        var events = new List<EventEnvelope>
        {
            CreateTestEnvelope(1), CreateTestEnvelope(2), CreateTestEnvelope(3),
            CreateTestEnvelope(4), CreateTestEnvelope(5),
        };

        int callCount = 0;
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                callCount++;
                if (callCount == 3) // Fail on 3rd event
                {
                    throw new HttpRequestException("Connection reset");
                }

                return Task.CompletedTask;
            });

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, events, "corr-001");

        // Assert
        result.Success.ShouldBeFalse();
        result.PublishedCount.ShouldBe(2); // Events 1-2 published, 3 failed
        result.FailureReason!.ShouldContain("Connection reset");
    }

    // --- Task 6.10: Empty event list ---

    [Fact]
    public async Task PublishEventsAsync_EmptyEventList_ReturnsSuccessWithZeroCount() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(TestIdentity, [], "corr-001");

        // Assert
        result.Success.ShouldBeTrue();
        result.PublishedCount.ShouldBe(0);
        result.FailureReason.ShouldBeNull();
        await daprClient.DidNotReceive().PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    // --- Task 6.11: OpenTelemetry activity ---

    [Fact]
    public async Task PublishEventsAsync_CreatesOpenTelemetryActivity_WithCorrectTags() {
        // Arrange
        (EventPublisher publisher, _, _) = CreatePublisher();
        var events = new List<EventEnvelope> { CreateTestEnvelope(), CreateTestEnvelope(2) };
        string expectedCorrelationId = $"corr-activity-{Guid.NewGuid():N}";
        Activity? capturedActivity = null;

        using var listener2 = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.OperationName == EventStoreActivitySource.EventsPublish
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), expectedCorrelationId)) {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener2);

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, events, expectedCorrelationId);

        // Assert
        _ = capturedActivity.ShouldNotBeNull();
        capturedActivity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldBe(expectedCorrelationId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("test-tenant");
        capturedActivity.GetTagItem(EventStoreActivitySource.TagDomain).ShouldBe("test-domain");
        capturedActivity.GetTagItem(EventStoreActivitySource.TagAggregateId).ShouldBe("agg-001");
        capturedActivity.GetTagItem(EventStoreActivitySource.TagEventCount).ShouldBe(2);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTopic).ShouldBe("test-tenant.test-domain.events");
    }

    // --- Task 6.12: Structured logging -- success ---

    [Fact]
    public async Task PublishEventsAsync_LogsSuccess_WithoutPayloadData() {
        // Arrange
        (EventPublisher publisher, _, ILogger<EventPublisher> logger) = CreatePublisher();
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-log");

        // Assert -- logs at Information level
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("Events published") &&
                o.ToString()!.Contains("corr-log") &&
                o.ToString()!.Contains("test-tenant") &&
                o.ToString()!.Contains("test-domain") &&
                !o.ToString()!.Contains("Payload")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // --- Story 4.1 Gap 6.1: Multi-tenant topic derivation ---

    [Fact]
    public async Task PublishEventsAsync_DifferentTenants_ProduceDifferentTopics() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        var acmeIdentity = new AggregateIdentity("acme", "orders", "order-1");
        var contosoIdentity = new AggregateIdentity("contoso", "orders", "order-1");
        EventEnvelope acmeEnvelope = new(
            "msg-1", "order-1", "test-aggregate", "acme", "orders", 1, 0, DateTimeOffset.UtcNow,
            "corr-acme", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json", [1], null);
        EventEnvelope contosoEnvelope = new(
            "msg-2", "order-1", "test-aggregate", "contoso", "orders", 1, 0, DateTimeOffset.UtcNow,
            "corr-contoso", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json", [1], null);

        // Act
        _ = await publisher.PublishEventsAsync(acmeIdentity, [acmeEnvelope], "corr-acme");
        _ = await publisher.PublishEventsAsync(contosoIdentity, [contosoEnvelope], "corr-contoso");

        // Assert -- different tenants produce different topics
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            "acme.orders.events",
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            "contoso.orders.events",
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Story 4.1 Gap 6.2: Multi-domain topic derivation ---

    [Fact]
    public async Task PublishEventsAsync_DifferentDomains_ProduceDifferentTopics() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        var paymentsIdentity = new AggregateIdentity("acme", "payments", "pay-1");
        var ordersIdentity = new AggregateIdentity("acme", "orders", "order-1");
        EventEnvelope paymentsEnvelope = new(
            "msg-1", "pay-1", "test-aggregate", "acme", "payments", 1, 0, DateTimeOffset.UtcNow,
            "corr-pay", "cause-1", "user-1", "1.0.0", "PaymentReceived", 1, "json", [1], null);
        EventEnvelope ordersEnvelope = new(
            "msg-2", "order-1", "test-aggregate", "acme", "orders", 1, 0, DateTimeOffset.UtcNow,
            "corr-ord", "cause-1", "user-1", "1.0.0", "OrderCreated", 1, "json", [1], null);

        // Act
        _ = await publisher.PublishEventsAsync(paymentsIdentity, [paymentsEnvelope], "corr-pay");
        _ = await publisher.PublishEventsAsync(ordersIdentity, [ordersEnvelope], "corr-ord");

        // Assert -- same tenant, different domains produce different topics
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            "acme.payments.events",
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            "acme.orders.events",
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Story 4.1 Gap 6.5: Boundary tenant IDs through publisher flow ---

    [Theory]
    [InlineData("a", "x", "a.x.events")]                                             // shortest valid tenant + domain
    [InlineData("my-tenant", "my-domain", "my-tenant.my-domain.events")]              // hyphenated
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz01",    // 64-char tenant (max)
                "orders",
                "abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz01.orders.events")]
    public async Task PublishEventsAsync_BoundaryTenantIds_ProducesValidTopics(string tenantId, string domain, string expectedTopic) {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, _) = CreatePublisher();
        var identity = new AggregateIdentity(tenantId, domain, "agg-1");
        EventEnvelope envelope = new(
            "msg-1", "agg-1", "test-aggregate", tenantId, domain, 1, 0, DateTimeOffset.UtcNow,
            "corr-boundary", "cause-1", "user-1", "1.0.0", "TestEvent", 1, "json", [1], null);

        // Act
        EventPublishResult result = await publisher.PublishEventsAsync(identity, [envelope], "corr-boundary");

        // Assert
        result.Success.ShouldBeTrue();
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            expectedTopic,
            Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 6.13: Structured logging -- failure ---

    [Fact]
    public async Task PublishEventsAsync_LogsFailure_WithCorrelationIdAndTopic() {
        // Arrange
        (EventPublisher publisher, DaprClient daprClient, ILogger<EventPublisher> logger) = CreatePublisher();
        _ = daprClient.PublishEventAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<EventEnvelope>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Broker offline"));
        EventEnvelope envelope = CreateTestEnvelope();

        // Act
        _ = await publisher.PublishEventsAsync(TestIdentity, [envelope], "corr-fail");

        // Assert -- logs at Error level
        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o =>
                o.ToString()!.Contains("publication failed") &&
                o.ToString()!.Contains("corr-fail") &&
                o.ToString()!.Contains("test-tenant.test-domain.events")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
