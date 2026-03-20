
using System.Text.Json;

using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Sample.Counter.Projections;

namespace Hexalith.EventStore.Sample.Tests.Counter.Projections;

public class CounterProjectionHandlerTests
{
    private static ProjectionEventDto CreateEvent(string eventTypeName)
        => new(
            EventTypeName: eventTypeName,
            Payload: System.Text.Encoding.UTF8.GetBytes("{}"),
            SerializationFormat: "json",
            SequenceNumber: 1,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "test-corr");

    private static ProjectionRequest CreateRequest(params ProjectionEventDto[] events)
        => new(
            TenantId: "sample-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            Events: events);

    [Fact]
    public void Project_SingleIncrement_ReturnsCountOne()
    {
        ProjectionRequest request = CreateRequest(CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal("counter", response.ProjectionType);
        Assert.Equal(1, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_MultipleIncrements_ReturnsCorrectCount()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(3, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_IncrementThenDecrement_ReturnsZero()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterDecremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(0, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_IncrementThenReset_ReturnsZero()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterReset"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(0, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_EmptyEvents_ReturnsZeroCount()
    {
        ProjectionRequest request = CreateRequest();

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal("counter", response.ProjectionType);
        Assert.Equal(0, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_RejectionEvent_Skipped()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterCannotGoNegative"),
            CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(2, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_ProjectionType_IsCounter()
    {
        ProjectionRequest request = CreateRequest(CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal("counter", response.ProjectionType);
    }

    [Fact]
    public void Project_MixedStreamWithClose_ReturnsPreCloseCount()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterClosed"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(2, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_FullyQualifiedEventTypeName_StillMatches()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("Hexalith.EventStore.Sample.Counter.Events.CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(1, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_NullRequest_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => CounterProjectionHandler.Project(null!));
    }

    [Fact]
    public void Project_NullEventTypeName_SkippedGracefully()
    {
        ProjectionEventDto nullTypeEvent = new(
            EventTypeName: null!,
            Payload: System.Text.Encoding.UTF8.GetBytes("{}"),
            SerializationFormat: "json",
            SequenceNumber: 2,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "test-corr");

        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            nullTypeEvent,
            CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(2, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_ResponseSurvivesJsonRoundTrip()
    {
        ProjectionRequest request = CreateRequest(CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        string json = JsonSerializer.Serialize(response);
        ProjectionResponse? deserialized = JsonSerializer.Deserialize<ProjectionResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("counter", deserialized.ProjectionType);
        Assert.Equal(1, deserialized.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_PostTombstoneEventsInStream_StillCounted()
    {
        ProjectionRequest request = CreateRequest(
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterIncremented"),
            CreateEvent("CounterClosed"),
            CreateEvent("AggregateTerminated"),
            CreateEvent("CounterIncremented"));

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal(3, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_NullEventsArray_ReturnsZeroCount()
    {
        ProjectionRequest request = new(
            TenantId: "sample-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            Events: null!);

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal("counter", response.ProjectionType);
        Assert.Equal(0, response.State.GetProperty("count").GetInt32());
    }

    [Fact]
    public void Project_NullEventElement_SkippedGracefully()
    {
        ProjectionRequest request = new(
            TenantId: "sample-tenant",
            Domain: "counter",
            AggregateId: "counter-1",
            Events: [CreateEvent("CounterIncremented"), null!, CreateEvent("CounterIncremented")]);

        ProjectionResponse response = CounterProjectionHandler.Project(request);

        Assert.Equal("counter", response.ProjectionType);
        Assert.Equal(2, response.State.GetProperty("count").GetInt32());
    }
}
