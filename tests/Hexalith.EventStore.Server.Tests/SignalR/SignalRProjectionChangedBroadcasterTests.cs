using Hexalith.EventStore.CommandApi.SignalR;

using System.Diagnostics;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.SignalR;

public class SignalRProjectionChangedBroadcasterTests {
    [Fact]
    public async Task BroadcastChangedAsync_ValidInput_ForwardsToGroupClient() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        hubContext.Clients.Returns(clients);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());

        await sut.BroadcastChangedAsync("order-list", "acme");

        await projectionClient.Received(1).ProjectionChanged("order-list", "acme");
    }

    [Fact]
    public async Task BroadcastChangedAsync_ClientFailure_DoesNotThrow() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        projectionClient
            .ProjectionChanged("order-list", "acme")
            .Returns(_ => throw new InvalidOperationException("SignalR down"));

        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        hubContext.Clients.Returns(clients);

        var sut = new SignalRProjectionChangedBroadcaster(hubContext, Substitute.For<ILogger<SignalRProjectionChangedBroadcaster>>());

        await Should.NotThrowAsync(() =>
            sut.BroadcastChangedAsync("order-list", "acme"));
    }

    [Fact]
    public async Task BroadcastChangedAsync_P99Dispatch_RemainsUnder100Milliseconds() {
        IProjectionChangedClient projectionClient = Substitute.For<IProjectionChangedClient>();
        IHubClients<IProjectionChangedClient> clients = Substitute.For<IHubClients<IProjectionChangedClient>>();
        clients.Group("order-list:acme").Returns(projectionClient);

        IHubContext<ProjectionChangedHub, IProjectionChangedClient> hubContext = Substitute.For<IHubContext<ProjectionChangedHub, IProjectionChangedClient>>();
        hubContext.Clients.Returns(clients);

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

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile) {
        values.Count.ShouldBeGreaterThan(0);

        List<double> sorted = [.. values.OrderBy(v => v)];
        int index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}