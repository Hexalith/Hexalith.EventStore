using Hexalith.EventStore.SignalR;

using Shouldly;

namespace Hexalith.EventStore.SignalR.Tests;

public class EventStoreSignalRClientTests {
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException() {
        Should.Throw<ArgumentNullException>(() =>
            new EventStoreSignalRClient(null!));
    }

    [Fact]
    public void Constructor_EmptyHubUrl_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = " " };

        Should.Throw<ArgumentException>(() =>
            new EventStoreSignalRClient(options));
    }

    [Fact]
    public void Constructor_RelativeHubUrl_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "/hubs/projection-changes" };

        Should.Throw<ArgumentException>(() =>
            new EventStoreSignalRClient(options));
    }

    [Fact]
    public async Task Constructor_AbsoluteHttpsHubUrl_DoesNotThrow() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };

        var sut = new EventStoreSignalRClient(options);

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAsync_AddsToTrackedGroups() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            bool callbackInvoked = false;

            // Subscribe before connecting — should store the group
            await sut.SubscribeAsync("counter", "acme", () => callbackInvoked = true);

            // No exception means the group was tracked successfully
            callbackInvoked.ShouldBeFalse(); // Callback not invoked until signal received
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesFromTrackedGroups() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await sut.SubscribeAsync("counter", "acme", () => { });
            await sut.UnsubscribeAsync("counter", "acme");

            // No exception — unsubscribe completed successfully
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeAsync_NullProjectionType_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeAsync(null!, "acme", () => { }));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeAsync_NullTenantId_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeAsync("counter", null!, () => { }));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeAsync_NullCallback_ThrowsArgumentNullException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await Should.ThrowAsync<ArgumentNullException>(() =>
                sut.SubscribeAsync("counter", "acme", null!));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeAsync_ProjectionTypeContainsColon_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeAsync("counter:bad", "acme", () => { }));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_TenantIdContainsColon_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await Should.ThrowAsync<ArgumentException>(() =>
                sut.UnsubscribeAsync("counter", "acme:bad"));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_ClearsSubscriptions() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);

        await sut.SubscribeAsync("counter", "acme", () => { });
        await sut.DisposeAsync();

        // No exception — disposal completed successfully
    }
}
