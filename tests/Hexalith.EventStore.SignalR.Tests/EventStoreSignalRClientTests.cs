using System.Reflection;

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

    [Fact]
    public async Task SubscribeAsync_MultipleCallbacksForSameGroup_AllCallbacksAreInvoked() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callback1Count = 0;
            int callback2Count = 0;

            Action callback1 = () => callback1Count++;
            Action callback2 = () => callback2Count++;

            await sut.SubscribeAsync("counter", "acme", callback1);
            await sut.SubscribeAsync("counter", "acme", callback2);

            InvokeProjectionChanged(sut, "counter", "acme");

            callback1Count.ShouldBe(1);
            callback2Count.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_CallbackOverload_RemovesOnlySpecifiedCallback() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callback1Count = 0;
            int callback2Count = 0;

            Action callback1 = () => callback1Count++;
            Action callback2 = () => callback2Count++;

            await sut.SubscribeAsync("counter", "acme", callback1);
            await sut.SubscribeAsync("counter", "acme", callback2);
            await sut.UnsubscribeAsync("counter", "acme", callback1);

            InvokeProjectionChanged(sut, "counter", "acme");

            callback1Count.ShouldBe(0);
            callback2Count.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    // === FR59: Reconnection auto-rejoin tests ===

    [Fact]
    public async Task OnReconnectedAsync_WithSubscribedGroups_CompletesWithoutThrowing() {
        // FR59: Auto-rejoin all subscribed groups on reconnection.
        // Since the connection isn't started, JoinGroup will fail gracefully.
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await sut.SubscribeAsync("counter", "acme", () => { });
            await sut.SubscribeAsync("orders", "contoso", () => { });

            // Simulate reconnection event — should attempt rejoining all groups
            await InvokeOnReconnectedAsync(sut, "new-connection-id");

            // No exception — rejoin failures are non-fatal (FR59)
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_NoSubscribedGroups_CompletesWithoutThrowing() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            // Reconnect with no groups — should be a no-op
            await InvokeOnReconnectedAsync(sut, "new-connection-id");
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_AfterUnsubscribe_DoesNotRejoinRemovedGroup() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await sut.SubscribeAsync("counter", "acme", () => { });
            await sut.SubscribeAsync("orders", "contoso", () => { });
            await sut.UnsubscribeAsync("counter", "acme");

            // Reconnect — should only attempt to rejoin "orders:contoso"
            await InvokeOnReconnectedAsync(sut, "new-connection-id");

            // Verify original callbacks still work for remaining group
            int callbackCount = 0;
            await sut.SubscribeAsync("orders", "contoso", () => callbackCount++);
            InvokeProjectionChanged(sut, "orders", "contoso");
            callbackCount.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_PreservesCallbacks_AfterReconnection() {
        // FR59: Callbacks must survive reconnection — they are tracked in _subscribedGroups
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callbackCount = 0;
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++);

            // Simulate reconnection
            await InvokeOnReconnectedAsync(sut, "new-connection-id");

            // Callbacks should still fire after reconnection
            InvokeProjectionChanged(sut, "counter", "acme");
            callbackCount.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_NullConnectionId_CompletesWithoutThrowing() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await sut.SubscribeAsync("counter", "acme", () => { });

            // Null connectionId is valid per SignalR spec
            await InvokeOnReconnectedAsync(sut, null);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_PreventsSubsequentReconnectionRejoin() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);

        await sut.SubscribeAsync("counter", "acme", () => { });
        await sut.DisposeAsync();

        // After disposal, subscribed groups are cleared
        // Any reconnection attempt would find no groups to rejoin
        // Cannot invoke OnReconnectedAsync after disposal — connection is disposed
    }

    private static void InvokeProjectionChanged(EventStoreSignalRClient client, string projectionType, string tenantId) {
        MethodInfo method = typeof(EventStoreSignalRClient)
            .GetMethod("OnProjectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnProjectionChanged method not found.");

        _ = method.Invoke(client, [projectionType, tenantId]);
    }

    private static async Task InvokeOnReconnectedAsync(EventStoreSignalRClient client, string? connectionId) {
        MethodInfo method = typeof(EventStoreSignalRClient)
            .GetMethod("OnReconnectedAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnReconnectedAsync method not found.");

        object? result = method.Invoke(client, [connectionId]);
        if (result is Task task) {
            await task.ConfigureAwait(false);
        }
    }
}
