namespace Hexalith.EventStore.Server.Tests.Telemetry;

using System.Diagnostics;

using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Server.Telemetry;

using Shouldly;

/// <summary>
/// Story 6.1 Task 9: CommandApi controller activity tests.
/// Verifies that the CommandApi ActivitySource creates activities with correct names and tags.
/// Uses direct ActivitySource testing to avoid full controller pipeline setup.
/// Each test uses a unique correlation ID to avoid parallel test interference.
/// </summary>
public class CommandApiTraceTests
{
    [Fact]
    public void SubmitCommand_CreatesSubmitActivity()
    {
        // Arrange
        string correlationId = $"submit-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == EventStoreActivitySources.Submit
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
                {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Act -- simulate what LoggingBehavior does
        using (Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
            EventStoreActivitySources.Submit))
        {
            activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
            activity?.SetTag(EventStoreActivitySource.TagTenantId, "test-tenant");
            activity?.SetTag(EventStoreActivitySource.TagDomain, "test-domain");
            activity?.SetTag(EventStoreActivitySource.TagCommandType, "CreateOrder");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        // Assert
        capturedActivity.ShouldNotBeNull();
        capturedActivity.OperationName.ShouldBe("EventStore.CommandApi.Submit");
        capturedActivity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldBe(correlationId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("test-tenant");
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public void QueryStatus_CreatesQueryStatusActivity()
    {
        // Arrange
        string correlationId = $"query-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == EventStoreActivitySources.QueryStatus
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
                {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Act -- simulate what CommandStatusController does
        using (Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
            EventStoreActivitySources.QueryStatus, ActivityKind.Server))
        {
            activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
            activity?.SetTag(EventStoreActivitySource.TagTenantId, "test-tenant");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        // Assert
        capturedActivity.ShouldNotBeNull();
        capturedActivity.OperationName.ShouldBe("EventStore.CommandApi.QueryStatus");
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldBe(correlationId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("test-tenant");
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public void ReplayCommand_CreatesReplayActivity()
    {
        // Arrange
        string correlationId = $"replay-{Guid.NewGuid()}";
        Activity? capturedActivity = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == EventStoreActivitySources.Replay
                    && Equals(activity.GetTagItem(EventStoreActivitySource.TagCorrelationId), correlationId))
                {
                    capturedActivity = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Act -- simulate what ReplayController does
        using (Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
            EventStoreActivitySources.Replay, ActivityKind.Server))
        {
            activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);
            activity?.SetTag(EventStoreActivitySource.TagTenantId, "test-tenant");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        // Assert
        capturedActivity.ShouldNotBeNull();
        capturedActivity.OperationName.ShouldBe("EventStore.CommandApi.Replay");
        capturedActivity.Kind.ShouldBe(ActivityKind.Server);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldBe(correlationId);
        capturedActivity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("test-tenant");
        capturedActivity.Status.ShouldBe(ActivityStatusCode.Ok);
    }

    [Fact]
    public void ControllerActivities_IncludeCorrelationIdTag()
    {
        // Arrange
        string testId = Guid.NewGuid().ToString();
        List<Activity> capturedActivities = [];
        string[] activityNames = [EventStoreActivitySources.Submit, EventStoreActivitySources.QueryStatus, EventStoreActivitySources.Replay];

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Hexalith.EventStore.CommandApi",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activityNames.Contains(activity.OperationName)
                    && activity.GetTagItem(EventStoreActivitySource.TagCorrelationId) is string corr
                    && corr.StartsWith($"corr-{testId}-", StringComparison.Ordinal))
                {
                    capturedActivities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        // Act -- create all three controller activities
        foreach (string name in activityNames)
        {
            using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(name, ActivityKind.Server);
            activity?.SetTag(EventStoreActivitySource.TagCorrelationId, $"corr-{testId}-{name}");
            activity?.SetTag(EventStoreActivitySource.TagTenantId, "test-tenant");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }

        // Assert -- all have correlation ID tag
        capturedActivities.Count.ShouldBe(3);
        foreach (Activity activity in capturedActivities)
        {
            activity.GetTagItem(EventStoreActivitySource.TagCorrelationId).ShouldNotBeNull(
                $"Activity '{activity.OperationName}' should have correlation ID tag");
            activity.GetTagItem(EventStoreActivitySource.TagTenantId).ShouldBe("test-tenant");
        }
    }
}
