using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public class NoOpProjectionChangedBroadcasterTests {
    [Fact]
    public void BroadcastChangedAsync_ReturnsSynchronouslyCompletedTask() {
        var sut = new NoOpProjectionChangedBroadcaster();

        Task task = sut.BroadcastChangedAsync("order-list", "acme");

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void BroadcastChangedAsync_WithCancellationToken_ReturnsSynchronouslyCompletedTask() {
        var sut = new NoOpProjectionChangedBroadcaster();
        using CancellationTokenSource cts = new();

        Task task = sut.BroadcastChangedAsync("order-list", "acme", cts.Token);

        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public void NoOpBroadcaster_ImplementsInterface() {
        var sut = new NoOpProjectionChangedBroadcaster();

        sut.ShouldBeAssignableTo<IProjectionChangedBroadcaster>();
    }
}
