namespace Hexalith.EventStore.Server.Tests.Events;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

public class EventStreamReaderTests
{
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    private static EventEnvelope CreateTestEvent(int seq) => new(
        AggregateId: "agg-001",
        TenantId: "test-tenant",
        Domain: "test-domain",
        SequenceNumber: seq,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: $"corr-{seq}",
        CausationId: $"cause-{seq}",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "OrderCreated",
        SerializationFormat: "json",
        Payload: [1, 2, 3],
        Extensions: null);

    private static (EventStreamReader Reader, IActorStateManager StateManager) CreateReader()
    {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<EventStreamReader>>();
        return (new EventStreamReader(stateManager, logger), stateManager);
    }

    private static void ConfigureNoMetadata(IActorStateManager stateManager, AggregateIdentity identity)
    {
        stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    private static void ConfigureMetadata(IActorStateManager stateManager, AggregateIdentity identity, long currentSequence)
    {
        var metadata = new AggregateMetadata(currentSequence, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
    }

    private static void ConfigureEvents(IActorStateManager stateManager, AggregateIdentity identity, int count)
    {
        string keyPrefix = identity.EventStreamKeyPrefix;
        for (int i = 1; i <= count; i++)
        {
            int seq = i; // capture for closure
            stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(seq)));
        }
    }

    [Fact]
    public async Task RehydrateAsync_NewAggregate_ReturnsNull()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureNoMetadata(stateManager, TestIdentity);

        // Act
        object? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RehydrateAsync_ExistingAggregate_ReadsEventsFromSequence1()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 3);
        ConfigureEvents(stateManager, TestIdentity, 3);

        // Act
        object? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        var events = result.ShouldBeOfType<List<EventEnvelope>>();
        events.Count.ShouldBe(3);
        events[0].SequenceNumber.ShouldBe(1);
        events[1].SequenceNumber.ShouldBe(2);
        events[2].SequenceNumber.ShouldBe(3);
    }

    [Fact]
    public async Task RehydrateAsync_ExistingAggregate_UsesCorrectKeyPattern()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 2);
        ConfigureEvents(stateManager, TestIdentity, 2);

        // Act
        await reader.RehydrateAsync(TestIdentity);

        // Assert -- verify composite key pattern {tenant}:{domain}:{aggId}:events:{seq}
        await stateManager.Received().TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:1", Arg.Any<CancellationToken>());
        await stateManager.Received().TryGetStateAsync<EventEnvelope>(
            "test-tenant:test-domain:agg-001:events:2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RehydrateAsync_ThousandEvents_CompletesWithin100ms()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 1000);

        // Configure all 1000 events to return immediately (mock -- no real I/O)
        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                string key = callInfo.Arg<string>();
                if (key.StartsWith(keyPrefix, StringComparison.Ordinal))
                {
                    int seq = int.Parse(key[keyPrefix.Length..]);
                    return new ConditionalValue<EventEnvelope>(true, CreateTestEvent(seq));
                }

                return new ConditionalValue<EventEnvelope>(false, default!);
            });

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        object? result = await reader.RehydrateAsync(TestIdentity);
        sw.Stop();

        // Assert
        var events = result.ShouldBeOfType<List<EventEnvelope>>();
        events.Count.ShouldBe(1000);
        sw.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task RehydrateAsync_MissingEvent_ThrowsMissingEventException()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 3);

        string keyPrefix = TestIdentity.EventStreamKeyPrefix;
        // Event 1 exists, event 2 missing, event 3 exists
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(1)));
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));
        stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}3", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateTestEvent(3)));

        // Act & Assert
        var ex = await Should.ThrowAsync<MissingEventException>(() => reader.RehydrateAsync(TestIdentity));
        ex.SequenceNumber.ShouldBe(2);
        ex.TenantId.ShouldBe("test-tenant");
        ex.Domain.ShouldBe("test-domain");
        ex.AggregateId.ShouldBe("agg-001");
    }

    [Fact]
    public async Task RehydrateAsync_InvalidMetadata_NegativeSequence_ThrowsException()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        var metadata = new AggregateMetadata(-1, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => reader.RehydrateAsync(TestIdentity));
        ex.Message.ShouldContain("CurrentSequence=-1");
    }

    [Fact]
    public async Task RehydrateAsync_InvalidMetadata_ZeroSequence_ThrowsException()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        var metadata = new AggregateMetadata(0, DateTimeOffset.UtcNow, null);
        stateManager.TryGetStateAsync<AggregateMetadata>(TestIdentity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => reader.RehydrateAsync(TestIdentity));
        ex.Message.ShouldContain("CurrentSequence=0");
    }

    [Fact]
    public async Task RehydrateAsync_NullIdentity_ThrowsArgumentNullException()
    {
        // Arrange
        (EventStreamReader reader, _) = CreateReader();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => reader.RehydrateAsync(null!));
    }

    [Fact]
    public async Task RehydrateAsync_EventsLoadedInOrder_VerifySequence()
    {
        // Arrange
        (EventStreamReader reader, IActorStateManager stateManager) = CreateReader();
        ConfigureMetadata(stateManager, TestIdentity, 5);
        ConfigureEvents(stateManager, TestIdentity, 5);

        // Act
        object? result = await reader.RehydrateAsync(TestIdentity);

        // Assert
        var events = result.ShouldBeOfType<List<EventEnvelope>>();
        for (int i = 0; i < events.Count; i++)
        {
            events[i].SequenceNumber.ShouldBe(i + 1);
        }
    }
}
