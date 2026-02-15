namespace Hexalith.EventStore.Server.Tests.Events;

using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

public class DeadLetterPublisherTests
{
    private static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string domain = "test-domain",
        string aggregateId = "agg-001") => new(
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: Guid.NewGuid().ToString(),
        CausationId: null,
        UserId: "system",
        Extensions: null);

    private static DeadLetterMessage CreateTestDeadLetterMessage(CommandEnvelope? command = null)
    {
        var cmd = command ?? CreateTestEnvelope();
        return DeadLetterMessage.FromException(
            cmd,
            CommandStatus.Processing,
            new InvalidOperationException("Test error"));
    }

    [Fact]
    public async Task PublishDeadLetter_Success_ReturnsTrueAndPublishesToCorrectTopic()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new EventPublisherOptions
        {
            PubSubName = "pubsub",
            DeadLetterTopicPrefix = "deadletter"
        });
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var message = CreateTestDeadLetterMessage();

        // Act
        var result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeTrue();
        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "deadletter.test-tenant.test-domain.events",
            message,
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishDeadLetter_Success_UsesCloudEventsMetadata()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new EventPublisherOptions
        {
            PubSubName = "pubsub",
            DeadLetterTopicPrefix = "deadletter"
        });
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var command = CreateTestEnvelope();
        var message = CreateTestDeadLetterMessage(command);

        Dictionary<string, string>? capturedMetadata = null;
        await daprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Do<Dictionary<string, string>>(m => capturedMetadata = m),
            Arg.Any<CancellationToken>());

        // Act
        await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        capturedMetadata.ShouldNotBeNull();
        capturedMetadata["cloudevent.type"].ShouldBe("deadletter.command.failed");
        capturedMetadata["cloudevent.source"].ShouldBe("eventstore/test-tenant/test-domain");
        capturedMetadata["cloudevent.id"].ShouldBe(message.CorrelationId);
        capturedMetadata["cloudevent.datacontenttype"].ShouldBe("application/json");
    }

    [Fact]
    public async Task PublishDeadLetter_Success_TopicFollowsDeadLetterPattern()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new EventPublisherOptions
        {
            PubSubName = "pubsub",
            DeadLetterTopicPrefix = "deadletter"
        });
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var message = CreateTestDeadLetterMessage();

        // Act
        await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            Arg.Any<string>(),
            "deadletter.test-tenant.test-domain.events",
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishDeadLetter_DaprThrows_ReturnsFalseNeverThrows()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Pub/sub unavailable"));

        var options = Options.Create(new EventPublisherOptions());
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var message = CreateTestDeadLetterMessage();

        // Act
        var result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishDeadLetter_DaprThrows_LogsError()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        daprClient.PublishEventAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Pub/sub unavailable"));

        var options = Options.Create(new EventPublisherOptions());
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var message = CreateTestDeadLetterMessage();

        // Act
        await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PublishDeadLetter_Success_CreatesOpenTelemetryActivity()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new EventPublisherOptions
        {
            PubSubName = "pubsub",
            DeadLetterTopicPrefix = "deadletter"
        });
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var command = CreateTestEnvelope();
        var message = DeadLetterMessage.FromException(
            command,
            CommandStatus.Processing,
            new InvalidOperationException("Boom"));

        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == EventStoreActivitySource.EventsPublishDeadLetter)
                {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        var result = await publisher.PublishDeadLetterAsync(identity, message);

        // Assert
        result.ShouldBeTrue();
        capturedActivity.ShouldNotBeNull();
        capturedActivity.OperationName.ShouldBe(EventStoreActivitySource.EventsPublishDeadLetter);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagCorrelationId)?.ToString()
            .ShouldBe(message.CorrelationId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId)?.ToString()
            .ShouldBe(identity.TenantId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagDomain)?.ToString()
            .ShouldBe(identity.Domain);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagAggregateId)?.ToString()
            .ShouldBe(identity.AggregateId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagCommandType)?.ToString()
            .ShouldBe(message.CommandType);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagFailureStage)?.ToString()
            .ShouldBe(message.FailureStage);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagExceptionType)?.ToString()
            .ShouldBe(message.ExceptionType);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagDeadLetterTopic)?.ToString()
            .ShouldBe("deadletter.test-tenant.test-domain.events");
    }

    [Fact]
    public async Task PublishDeadLetter_NeverLogsCommandPayload()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new EventPublisherOptions());
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        var command = CreateTestEnvelope();
        var message = CreateTestDeadLetterMessage(command);

        // Act
        await publisher.PublishDeadLetterAsync(identity, message);

        // Assert - verify logs don't contain payload bytes
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => !state.ToString()!.Contains("[1, 2, 3]") && !state.ToString()!.Contains("Payload")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task PublishDeadLetter_MultiTenant_EachTenantGetsOwnDeadLetterTopic()
    {
        // Arrange
        var daprClient = Substitute.For<DaprClient>();
        var options = Options.Create(new EventPublisherOptions
        {
            PubSubName = "pubsub",
            DeadLetterTopicPrefix = "deadletter"
        });
        var logger = Substitute.For<ILogger<DeadLetterPublisher>>();
        var publisher = new DeadLetterPublisher(daprClient, options, logger);

        var identity1 = new AggregateIdentity("tenant-a", "orders", "agg-001");
        var command1 = CreateTestEnvelope("tenant-a", "orders", "agg-001");
        var message1 = CreateTestDeadLetterMessage(command1);

        var identity2 = new AggregateIdentity("tenant-b", "inventory", "agg-002");
        var command2 = CreateTestEnvelope("tenant-b", "inventory", "agg-002");
        var message2 = CreateTestDeadLetterMessage(command2);

        // Act
        await publisher.PublishDeadLetterAsync(identity1, message1);
        await publisher.PublishDeadLetterAsync(identity2, message2);

        // Assert
        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "deadletter.tenant-a.orders.events",
            message1,
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());

        await daprClient.Received(1).PublishEventAsync(
            "pubsub",
            "deadletter.tenant-b.inventory.events",
            message2,
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }
}
