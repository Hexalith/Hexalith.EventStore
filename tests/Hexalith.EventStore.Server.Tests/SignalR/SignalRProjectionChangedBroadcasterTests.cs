using System.Diagnostics;

using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.SignalRHub;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.SignalR;

public class SignalRProjectionChangedBroadcasterTests {
    private const string BroadcastActivityName = "EventStore.SignalR.BroadcastProjectionChanged";

    [Fact]
    public async Task BroadcastChangedAsync_ValidInput_ForwardsToGroupClient() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());

        await sut.BroadcastChangedAsync("order-list", "acme");

        await projectionClient.Received(1).ProjectionChanged("order-list", "acme");
    }

    [Fact]
    public async Task BroadcastChangedAsync_Success_LogsStartAndCompletionEvidence() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var logEntries = new List<LogEntry>();
        var sut = new SignalRProjectionChangedBroadcaster(hubContext, new TestLogger<SignalRProjectionChangedBroadcaster>(logEntries));

        await sut.BroadcastChangedAsync("order-list", "acme");

        LogEntry start = logEntries.Single(e => e.EventId.Id == 1090);
        start.Level.ShouldBe(LogLevel.Information);
        start.Message.ShouldContain("order-list");
        start.Message.ShouldContain("acme");
        start.Message.ShouldContain("order-list:acme");
        start.Message.ShouldContain(nameof(SignalRProjectionChangedBroadcaster));

        LogEntry completion = logEntries.Single(e => e.EventId.Id == 1091);
        completion.Level.ShouldBe(LogLevel.Information);
        completion.Message.ShouldContain("order-list");
        completion.Message.ShouldContain("acme");
        completion.Message.ShouldContain("order-list:acme");
        completion.Message.ShouldContain("Success");
        completion.Message.ShouldContain("ElapsedMilliseconds");
        completion.Message.ShouldContain(nameof(SignalRProjectionChangedBroadcaster));
    }

    [Fact]
    public async Task BroadcastChangedAsync_Success_EmitsRepositoryActivityWithEvidenceTags() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var activities = new List<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());

        await sut.BroadcastChangedAsync("order-list", "acme");

        Activity activity = activities.Single(a => a.OperationName == BroadcastActivityName);
        activity.GetTagItem("eventstore.signalr.projection_type").ShouldBe("order-list");
        activity.GetTagItem("eventstore.signalr.tenant_id").ShouldBe("acme");
        activity.GetTagItem("eventstore.signalr.group_name").ShouldBe("order-list:acme");
        activity.GetTagItem("eventstore.signalr.result").ShouldBe("Success");
        activity.GetTagItem("eventstore.signalr.elapsed_ms").ShouldNotBeNull();
    }

    [Fact]
    public async Task BroadcastChangedAsync_ClientFailure_DoesNotThrow() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        _ = projectionClient
            .ProjectionChanged("order-list", "acme")
            .Returns(_ => throw new InvalidOperationException("SignalR down"));

        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());

        await Should.NotThrowAsync(() =>
            sut.BroadcastChangedAsync("order-list", "acme"));
    }

    [Fact]
    public async Task BroadcastChangedAsync_ClientFailure_LogsFailOpenEvidenceWithGroupAndElapsedTime() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        _ = projectionClient
            .ProjectionChanged("order-list", "acme")
            .Returns(_ => throw new InvalidOperationException("SignalR down"));

        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var logEntries = new List<LogEntry>();
        var sut = new SignalRProjectionChangedBroadcaster(hubContext, new TestLogger<SignalRProjectionChangedBroadcaster>(logEntries));

        await sut.BroadcastChangedAsync("order-list", "acme");

        LogEntry failure = logEntries.Single(e => e.EventId.Id == 1092);
        failure.Level.ShouldBe(LogLevel.Warning);
        failure.Message.ShouldContain("order-list");
        failure.Message.ShouldContain("acme");
        failure.Message.ShouldContain("order-list:acme");
        failure.Message.ShouldContain("InvalidOperationException");
        failure.Message.ShouldContain("ElapsedMilliseconds");
        failure.Message.ShouldContain("FailOpenFailure");
        failure.Message.ShouldContain(nameof(SignalRProjectionChangedBroadcaster));
    }

    [Fact]
    public async Task BroadcastChangedAsync_ClientFailure_ActivityRecordsFailOpenFailure() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        _ = projectionClient
            .ProjectionChanged("order-list", "acme")
            .Returns(_ => throw new InvalidOperationException("SignalR down"));

        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var activities = new List<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventStoreActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());

        await sut.BroadcastChangedAsync("order-list", "acme");

        Activity activity = activities.Single(a => a.OperationName == BroadcastActivityName);
        activity.GetTagItem("eventstore.signalr.result").ShouldBe("FailOpenFailure");
        activity.GetTagItem("eventstore.signalr.exception_type").ShouldBe("InvalidOperationException");
        activity.GetTagItem("eventstore.signalr.elapsed_ms").ShouldNotBeNull();
    }

    [Fact]
    public async Task BroadcastChangedAsync_P99Dispatch_RemainsUnder100Milliseconds() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        _ = clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        _ = hubContext.Clients.Returns(clients);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());
        const int warmupCount = 5;
        const int measuredCount = 50;
        List<double> dispatchTimesMs = [];

        for (int i = 0; i < warmupCount + measuredCount; i++) {
            long started = Stopwatch.GetTimestamp();

            await sut.BroadcastChangedAsync("order-list", "acme");

            if (i >= warmupCount) {
                dispatchTimesMs.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }

        dispatchTimesMs.Count.ShouldBe(measuredCount);
        CalculatePercentile(dispatchTimesMs, 0.99).ShouldBeLessThan(100d);
    }

    [Fact]
    public void BroadcasterEvidenceEventIds_DoNotOverlapHubEventIds() {
        int[] broadcasterEventIds = [1090, 1091, 1092];
        int[] hubEventIds = [1080, 1081, 1082, 1083, 1084, 1085];

        broadcasterEventIds.ShouldAllBe(id => !hubEventIds.Contains(id));
    }

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile) {
        values.Count.ShouldBeGreaterThan(0);

        List<double> sorted = [.. values.OrderBy(v => v)];
        int index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
