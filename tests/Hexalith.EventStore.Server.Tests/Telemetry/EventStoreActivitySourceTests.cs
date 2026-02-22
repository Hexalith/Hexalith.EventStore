namespace Hexalith.EventStore.Server.Tests.Telemetry;

using System.Diagnostics;

using Hexalith.EventStore.Server.Telemetry;

using Shouldly;

/// <summary>
/// Story 3.11 Task 9: OpenTelemetry activity source tests.
/// Verifies activity creation, naming conventions, and tag constants.
/// </summary>
public class EventStoreActivitySourceTests {
    [Fact]
    public void Instance_CreatesActivitySpans_WhenListenerIsActive() {
        // Arrange -- register a listener to enable activity creation
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.ProcessCommand);

        // Assert
        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe(EventStoreActivitySource.ProcessCommand);
    }

    [Fact]
    public void Activities_IncludeCorrectTags_WhenSet() {
        // Arrange
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using Activity? activity = EventStoreActivitySource.Instance.StartActivity(
            EventStoreActivitySource.IdempotencyCheck);
        activity?.SetTag(EventStoreActivitySource.TagCorrelationId, "corr-123");
        activity?.SetTag(EventStoreActivitySource.TagTenantId, "tenant-a");
        activity?.SetTag(EventStoreActivitySource.TagDomain, "orders");
        activity?.SetTag(EventStoreActivitySource.TagAggregateId, "order-001");
        activity?.SetTag(EventStoreActivitySource.TagCommandType, "CreateOrder");

        // Assert
        activity.ShouldNotBeNull();
        activity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldBe("corr-123");
        activity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("tenant-a");
        activity.GetTagItem(EventStoreActivitySource.TagDomain).ShouldBe("orders");
        activity.GetTagItem(EventStoreActivitySource.TagAggregateId).ShouldBe("order-001");
        activity.GetTagItem(EventStoreActivitySource.TagCommandType).ShouldBe("CreateOrder");
    }

    [Theory]
    [InlineData(EventStoreActivitySource.ProcessCommand, "EventStore.Actor.")]
    [InlineData(EventStoreActivitySource.IdempotencyCheck, "EventStore.Actor.")]
    [InlineData(EventStoreActivitySource.TenantValidation, "EventStore.Actor.")]
    [InlineData(EventStoreActivitySource.StateRehydration, "EventStore.Actor.")]
    [InlineData(EventStoreActivitySource.DomainServiceInvoke, "EventStore.DomainService.")]
    [InlineData(EventStoreActivitySource.EventsPersist, "EventStore.Events.")]
    [InlineData(EventStoreActivitySource.EventsPublish, "EventStore.Events.")]
    [InlineData(EventStoreActivitySource.EventsDrain, "EventStore.Events.")]
    [InlineData(EventStoreActivitySource.EventsPublishDeadLetter, "EventStore.Events.")]
    [InlineData(EventStoreActivitySource.StateMachineTransition, "EventStore.Actor.")]
    public void Activities_FollowNamingConvention(string activityName, string expectedPrefix) {
        // Assert -- activity names follow EventStore.{Component}.{Action} pattern
        activityName.ShouldStartWith(expectedPrefix);
    }
}
