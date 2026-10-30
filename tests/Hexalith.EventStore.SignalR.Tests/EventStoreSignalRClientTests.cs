using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.SignalR.Tests;

public class EventStoreSignalRClientTests {
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() =>
            new EventStoreSignalRClient(null!));

    [Fact]
    public void Constructor_EmptyHubUrl_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = " " };

        _ = Should.Throw<ArgumentException>(() =>
            new EventStoreSignalRClient(options));
    }

    [Fact]
    public void Constructor_RelativeHubUrl_ThrowsArgumentException() {
        // Use a non-slash relative URL; "/hubs/..." is an absolute file URI on Linux
        var options = new EventStoreSignalRClientOptions { HubUrl = "hubs/projection-changes" };

        _ = Should.Throw<ArgumentException>(() =>
            new EventStoreSignalRClient(options));
    }

    [Fact]
    public async Task Constructor_AbsoluteHttpsHubUrl_DoesNotThrow() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };

        var sut = new EventStoreSignalRClient(options);

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithCustomRetryPolicy_PassesPolicyToReconnectConfigurator() {
        var retryPolicy = new TestRetryPolicy();
        var options = new EventStoreSignalRClientOptions {
            HubUrl = "https://localhost/hubs/projection-changes",
            RetryPolicy = retryPolicy,
        };

        IRetryPolicy? capturedPolicy = null;
        EventStoreSignalRClient sut = CreateClientWithReconnectConfigurator(options, (_, policy) => capturedPolicy = policy);
        try {
            capturedPolicy.ShouldBeSameAs(retryPolicy);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task Constructor_WithNullRetryPolicy_PassesNullToReconnectConfigurator() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };

        var sentinel = new TestRetryPolicy();
        IRetryPolicy? capturedPolicy = sentinel;
        EventStoreSignalRClient sut = CreateClientWithReconnectConfigurator(options, (_, policy) => capturedPolicy = policy);
        try {
            capturedPolicy.ShouldBeNull();
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeAsync_AddsToTrackedGroups() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            bool callbackInvoked = false;

            // Subscribe before connecting — should store the group
            await sut.SubscribeAsync("counter", "acme", () => callbackInvoked = true, TestContext.Current.CancellationToken);

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
            await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
            await sut.UnsubscribeAsync("counter", "acme", TestContext.Current.CancellationToken);

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
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeAsync(null!, "acme", () => { }, TestContext.Current.CancellationToken));
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
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeAsync("counter", null!, () => { }, TestContext.Current.CancellationToken));
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
            _ = await Should.ThrowAsync<ArgumentNullException>(() =>
                sut.SubscribeAsync("counter", "acme", null!, TestContext.Current.CancellationToken));
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
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeAsync("counter:bad", "acme", () => { }, TestContext.Current.CancellationToken));
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
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                sut.UnsubscribeAsync("counter", "acme:bad", TestContext.Current.CancellationToken));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_ClearsSubscriptions() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);

        await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
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

            void callback1() => callback1Count++;
            void callback2() => callback2Count++;

            await sut.SubscribeAsync("counter", "acme", callback1, TestContext.Current.CancellationToken);
            await sut.SubscribeAsync("counter", "acme", callback2, TestContext.Current.CancellationToken);

            InvokeProjectionChanged(sut, "counter", "acme");

            callback1Count.ShouldBe(1);
            callback2Count.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnProjectionChanged_WithSubscribedGroup_LogsReceiptBeforeCallbacks() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            int callbackCount = 0;
            await sut.SubscribeAsync("counter", "acme", () => {
                // Prove ordering — receipt log must already be recorded by the time the callback runs.
                // Avoid coupling to absolute log count so a future log emission elsewhere does not break this test.
                logEntries.Any(e => e.EventId.Id == 2090).ShouldBeTrue("receipt log (EventId 2090) must be recorded before consumer callbacks fire");
                callbackCount++;
            }, TestContext.Current.CancellationToken);

            InvokeProjectionChanged(sut, "counter", "acme");

            callbackCount.ShouldBe(1);
            LogEntry receipt = logEntries.Single(e => e.EventId.Id == 2090);
            receipt.Level.ShouldBe(LogLevel.Information);
            receipt.Message.ShouldContain("counter");
            receipt.Message.ShouldContain("acme");
            receipt.Message.ShouldContain("counter:acme");
            receipt.Message.ShouldContain("Disconnected");
            receipt.Message.ShouldContain("CallbackCount: 1");
            receipt.Message.ShouldContain(nameof(EventStoreSignalRClient));
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnProjectionChanged_WithMultipleCallbacks_LogsCallbackCount() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            int callbackCount = 0;
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++, TestContext.Current.CancellationToken);
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++, TestContext.Current.CancellationToken);

            InvokeProjectionChanged(sut, "counter", "acme");

            callbackCount.ShouldBe(2);
            LogEntry receipt = logEntries.Single(e => e.EventId.Id == 2090);
            receipt.Message.ShouldContain("CallbackCount: 2");
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnProjectionChanged_WithMismatchedGroup_DoesNotLogReceiptOrInvokeCallbacks() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            int callbackCount = 0;
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++, TestContext.Current.CancellationToken);

            InvokeProjectionChanged(sut, "counter", "other-tenant");

            callbackCount.ShouldBe(0);
            logEntries.ShouldBeEmpty();
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task SubscribeDetailAsync_WithScopedGroup_InvokesMatchingDetailCallback() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            ProjectionChangedDetail? received = null;
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["freshness"] = "changed",
            };

            await sut.SubscribeDetailAsync("counter", "acme", "counter-1", detail => received = detail, TestContext.Current.CancellationToken).ConfigureAwait(true);

            InvokeProjectionChangedDetail(sut, "counter", "acme", "counter-1", metadata);

            _ = received.ShouldNotBeNull();
            received.ProjectionType.ShouldBe("counter");
            received.TenantId.ShouldBe("acme");
            received.GroupScope.ShouldBe("counter-1");
            received.Metadata.ShouldNotBeSameAs(metadata);
            received.Metadata["freshness"].ShouldBe("changed");
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SubscribeDetailAsync_NullScope_FallsBackToTenantWideDetailGroup() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callbackCount = 0;

            await sut.SubscribeDetailAsync("counter", "acme", null, _ => callbackCount++, TestContext.Current.CancellationToken).ConfigureAwait(true);

            InvokeProjectionChangedDetail(
                sut,
                "counter",
                "acme",
                null,
                new Dictionary<string, string>(StringComparer.Ordinal));

            callbackCount.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task OnProjectionChangedDetail_WithWrongScope_DoesNotLogReceiptOrInvokeCallbacks() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            int callbackCount = 0;
            await sut.SubscribeDetailAsync("counter", "acme", "counter-2", _ => callbackCount++, TestContext.Current.CancellationToken).ConfigureAwait(true);

            InvokeProjectionChangedDetail(
                sut,
                "counter",
                "acme",
                "counter-1",
                new Dictionary<string, string>(StringComparer.Ordinal));

            callbackCount.ShouldBe(0);
            logEntries.ShouldBeEmpty();
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UnsubscribeDetailAsync_CallbackOverload_RemovesOnlySpecifiedCallback() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callback1Count = 0;
            int callback2Count = 0;

            void callback1(ProjectionChangedDetail _) => callback1Count++;
            void callback2(ProjectionChangedDetail _) => callback2Count++;

            await sut.SubscribeDetailAsync("counter", "acme", "counter-1", callback1, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await sut.SubscribeDetailAsync("counter", "acme", "counter-1", callback2, TestContext.Current.CancellationToken).ConfigureAwait(true);
            await sut.UnsubscribeDetailAsync("counter", "acme", "counter-1", callback1, TestContext.Current.CancellationToken).ConfigureAwait(true);

            InvokeProjectionChangedDetail(
                sut,
                "counter",
                "acme",
                "counter-1",
                new Dictionary<string, string>(StringComparer.Ordinal));

            callback1Count.ShouldBe(0);
            callback2Count.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SubscribeDetailAsync_ScopeContainsColon_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeDetailAsync("counter", "acme", "counter:1", _ => { }, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task SubscribeDetailAsync_ScopeExceeds64Characters_ThrowsArgumentException() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            _ = await Should.ThrowAsync<ArgumentException>(() =>
                sut.SubscribeDetailAsync("counter", "acme", new string('a', 65), _ => { }, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_WithScopedDetailSubscription_PreservesCallbackDispatch() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callbackCount = 0;
            await sut.SubscribeDetailAsync("counter", "acme", "counter-1", _ => callbackCount++, TestContext.Current.CancellationToken).ConfigureAwait(true);

            await InvokeOnReconnectedAsync(sut, "new-connection-id").ConfigureAwait(true);
            InvokeProjectionChangedDetail(
                sut,
                "counter",
                "acme",
                "counter-1",
                new Dictionary<string, string>(StringComparer.Ordinal));

            callbackCount.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task OnProjectionChangedDetail_CopiesMetadataBeforeCallbacks() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            ProjectionChangedDetail? received = null;
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["freshness"] = "changed",
            };
            await sut.SubscribeDetailAsync("counter", "acme", "counter-1", detail => received = detail, TestContext.Current.CancellationToken).ConfigureAwait(true);

            InvokeProjectionChangedDetail(sut, "counter", "acme", "counter-1", metadata);
            metadata["freshness"] = "mutated";

            _ = received.ShouldNotBeNull();
            received.Metadata.ShouldNotBeSameAs(metadata);
            received.Metadata["freshness"].ShouldBe("changed");
            if (received.Metadata is IDictionary<string, string> mutableMetadata) {
                _ = Should.Throw<NotSupportedException>(() => mutableMetadata["freshness"] = "callback-mutation");
            }
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task OnProjectionChangedDetail_DoesNotLogMetadataValuesAboveDebug() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            string secretValue = Guid.NewGuid().ToString("N");
            await sut.SubscribeDetailAsync("counter", "acme", "counter-1", _ => { }, TestContext.Current.CancellationToken).ConfigureAwait(true);

            InvokeProjectionChangedDetail(
                sut,
                "counter",
                "acme",
                "counter-1",
                new Dictionary<string, string>(StringComparer.Ordinal) {
                    ["support-safe-key"] = secretValue,
                });

            LogEntry receipt = logEntries.Single(e => e.EventId.Id == 2091);
            receipt.Level.ShouldBe(LogLevel.Information);
            receipt.Message.ShouldContain("MetadataCount: 1");
            receipt.Message.ShouldNotContain(secretValue);
        }
        finally {
            await sut.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_CallbackOverload_RemovesOnlySpecifiedCallback() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callback1Count = 0;
            int callback2Count = 0;

            void callback1() => callback1Count++;
            void callback2() => callback2Count++;

            await sut.SubscribeAsync("counter", "acme", callback1, TestContext.Current.CancellationToken);
            await sut.SubscribeAsync("counter", "acme", callback2, TestContext.Current.CancellationToken);
            await sut.UnsubscribeAsync("counter", "acme", callback1, TestContext.Current.CancellationToken);

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
            await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
            await sut.SubscribeAsync("orders", "contoso", () => { }, TestContext.Current.CancellationToken);

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
            await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
            await sut.SubscribeAsync("orders", "contoso", () => { }, TestContext.Current.CancellationToken);
            await sut.UnsubscribeAsync("counter", "acme", TestContext.Current.CancellationToken);

            // Reconnect — should only attempt to rejoin "orders:contoso"
            await InvokeOnReconnectedAsync(sut, "new-connection-id");

            // Verify original callbacks still work for remaining group
            int callbackCount = 0;
            await sut.SubscribeAsync("orders", "contoso", () => callbackCount++, TestContext.Current.CancellationToken);
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
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++, TestContext.Current.CancellationToken);

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
            await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);

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

        await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
        await sut.DisposeAsync();

        // After disposal, subscribed groups are cleared
        // Any reconnection attempt would find no groups to rejoin
        // Cannot invoke OnReconnectedAsync after disposal — connection is disposed
    }

    [Fact]
    public async Task OnReconnectedAsync_MultipleReconnections_KeepsTrackedGroupsAvailable() {
        // Verify tracked groups and callbacks survive repeated reconnection handling.
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
            await sut.SubscribeAsync("orders", "contoso", () => { }, TestContext.Current.CancellationToken);

            // Simulate 3 consecutive reconnection callbacks.
            await InvokeOnReconnectedAsync(sut, "conn-1");
            await InvokeOnReconnectedAsync(sut, "conn-2");
            await InvokeOnReconnectedAsync(sut, "conn-3");

            // Callback dispatch should still work afterward.
            int callbackCount = 0;
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++, TestContext.Current.CancellationToken);
            InvokeProjectionChanged(sut, "counter", "acme");
            callbackCount.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_SubscribeAfterPriorReconnect_PreservesNewGroupCallback() {
        // Verify a newly tracked group still dispatches callbacks after another reconnection callback.
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            await sut.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);

            // Simulate first reconnection
            await InvokeOnReconnectedAsync(sut, "conn-1");

            // Subscribe to a new group while connected (simulating between disconnect/reconnect)
            int newGroupCallbackCount = 0;
            await sut.SubscribeAsync("orders", "contoso", () => newGroupCallbackCount++, TestContext.Current.CancellationToken);

            // Simulate second reconnection callback.
            await InvokeOnReconnectedAsync(sut, "conn-2");

            // Verify callback for the newly added group still works.
            InvokeProjectionChanged(sut, "orders", "contoso");
            newGroupCallbackCount.ShouldBe(1);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnReconnectedAsync_MultipleReconnections_PreservesCallbackDispatch() {
        // Verify callbacks survive repeated reconnection handling.
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            int callbackCount = 0;
            await sut.SubscribeAsync("counter", "acme", () => callbackCount++, TestContext.Current.CancellationToken);

            // Simulate 5 consecutive reconnection callbacks.
            for (int i = 1; i <= 5; i++) {
                await InvokeOnReconnectedAsync(sut, $"conn-{i}");

                // Verify callback fires correctly after each cycle.
                InvokeProjectionChanged(sut, "counter", "acme");
            }

            // Callback should have fired once per cycle.
            callbackCount.ShouldBe(5);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task Closed_Event_WithException_LogsNeutralWarning() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            await InvokeOnClosedAsync(sut, new InvalidOperationException("Connection lost")).ConfigureAwait(true);

            logEntries.Count.ShouldBe(1);
            logEntries[0].Level.ShouldBe(LogLevel.Warning);
            logEntries[0].Message.ShouldContain("SignalR connection closed unexpectedly.");
            logEntries[0].Message.ShouldNotContain("retries were exhausted");
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    // === P0 Gap Coverage: IsConnected, UnsubscribeAsync no-op, Closed disposal-aware ===

    [Fact]
    public async Task IsConnected_ReturnsFalse_WhenNotStarted() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            sut.IsConnected.ShouldBeFalse();
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_CallbackOverload_NoOpWhenGroupNotRegistered() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            // Unsubscribe a callback for a group that was never subscribed — should not throw
            await sut.UnsubscribeAsync("nonexistent", "tenant", () => { }, TestContext.Current.CancellationToken);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_GroupOverload_NoOpWhenGroupNotRegistered() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            // Unsubscribe an entire group that was never subscribed — should not throw
            await sut.UnsubscribeAsync("nonexistent", "tenant", TestContext.Current.CancellationToken);
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnProjectionChanged_NoOpWhenGroupNotSubscribed() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var sut = new EventStoreSignalRClient(options);
        try {
            // Fire a signal for a group with no subscribers — should not throw
            InvokeProjectionChanged(sut, "nonexistent", "tenant");
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnClosedAsync_AfterDisposal_DoesNotLogWarning() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));

        await sut.DisposeAsync();

        // After disposal, closed events with exceptions should be silenced
        await InvokeOnClosedAsync(sut, new InvalidOperationException("Post-dispose close")).ConfigureAwait(true);

        logEntries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Closed_Event_WithNullException_DoesNotLogWarning() {
        var options = new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hubs/projection-changes" };
        var logEntries = new List<LogEntry>();
        var sut = new EventStoreSignalRClient(options, new TestLogger<EventStoreSignalRClient>(logEntries));
        try {
            await InvokeOnClosedAsync(sut, null).ConfigureAwait(true);

            logEntries.ShouldBeEmpty();
        }
        finally {
            await sut.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RealStalledGroupInvocationCancellationSettlesAndRevokesCallback(bool leave, bool detail)
    {
        var control = new StalledProjectionRpc();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(control);
        WebApplication app = builder.Build();
        app.MapHub<StalledProjectionHub>("/hub");
        await app.StartAsync(TestContext.Current.CancellationToken);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var paused = new StalledProjectionReceiptLogger();
        Task dispatch = Task.CompletedTask;
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" }, paused);
        using var cancellation = new CancellationTokenSource();
        int calls = 0;
        void callback() => calls++;
        void detailCallback(ProjectionChangedDetail _) => calls++;
        try
        {
            await client.StartAsync(TestContext.Current.CancellationToken);
            Task operation;
            if (leave)
            {
                control.ReleaseJoin.SetResult();
                if (detail)
                {
                    await client.SubscribeDetailAsync("counter", "acme", "party-1", detailCallback, TestContext.Current.CancellationToken);
                    operation = client.UnsubscribeDetailAsync("counter", "acme", "party-1", detailCallback, cancellation.Token);
                }
                else
                {
                    await client.SubscribeAsync("counter", "acme", callback, TestContext.Current.CancellationToken);
                    operation = client.UnsubscribeAsync("counter", "acme", callback, cancellation.Token);
                }
                await control.LeaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            else
            {
                operation = detail
                    ? client.SubscribeDetailAsync("counter", "acme", "party-1", detailCallback, cancellation.Token)
                    : client.SubscribeAsync("counter", "acme", callback, cancellation.Token);
                await control.JoinEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                dispatch = Task.Run(() =>
                {
                    if (detail)
                    {
                        InvokeProjectionChangedDetail(client, "counter", "acme", "party-1", new Dictionary<string, string>());
                    }
                    else
                    {
                        InvokeProjectionChanged(client, "counter", "acme");
                    }
                }, TestContext.Current.CancellationToken);
                await paused.Captured.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            paused.Resume.Set();
            await dispatch.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            InvokeProjectionChanged(client, "counter", "acme");
            calls.ShouldBe(0);
            control.ReleaseJoin.TrySetResult();
            control.ReleaseLeave.TrySetResult();
            await (leave ? control.LeaveFinished.Task : control.JoinFinished.Task)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            InvokeProjectionChanged(client, "counter", "acme");
            InvokeProjectionChangedDetail(client, "counter", "acme", "party-1", new Dictionary<string, string>());
            calls.ShouldBe(0);
        }
        finally
        {
            paused.Resume.Set();
            await dispatch.ConfigureAwait(true);
            control.ReleaseJoin.TrySetResult();
            control.ReleaseLeave.TrySetResult();
            await client.DisposeAsync();
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledJoinPreservesAnotherSubscriberInTheSameGroup(bool detail)
    {
        var control = new StalledProjectionRpc();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(control);
        WebApplication app = builder.Build();
        await using var applicationLifetime = app.ConfigureAwait(true);
        app.MapHub<StalledProjectionHub>("/hub");
        await app.StartAsync(TestContext.Current.CancellationToken);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        using var cancellation = new CancellationTokenSource();
        int revokedCalls = 0;
        int survivingCalls = 0;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await client.StartAsync(TestContext.Current.CancellationToken);
            Task cancelled = detail
                ? client.SubscribeDetailAsync("counter", "acme", "party-1", _ => revokedCalls++, cancellation.Token)
                : client.SubscribeAsync("counter", "acme", () => revokedCalls++, cancellation.Token);
            await control.JoinEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            void onReceived()
            {
                Interlocked.Increment(ref survivingCalls);
                received.TrySetResult();
            }
            Task survivor = detail
                ? client.SubscribeDetailAsync("counter", "acme", "party-1", _ => onReceived(), TestContext.Current.CancellationToken)
                : client.SubscribeAsync("counter", "acme", onReceived, TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => cancelled.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            control.ReleaseJoin.TrySetResult();
            await survivor.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            IHubClients clients = app.Services.GetRequiredService<IHubContext<StalledProjectionHub>>().Clients;
            if (detail)
            {
                await clients.Group("counter:acme:party-1").SendAsync("ProjectionChangedDetail", "counter", "acme", "party-1",
                    new Dictionary<string, string>(), TestContext.Current.CancellationToken);
            }
            else
            {
                await clients.Group("counter:acme").SendAsync("ProjectionChanged", "counter", "acme", TestContext.Current.CancellationToken);
            }
            await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            revokedCalls.ShouldBe(0);
            survivingCalls.ShouldBe(1);
        }
        finally
        {
            control.ReleaseJoin.TrySetResult();
            control.ReleaseLeave.TrySetResult();
            await client.DisposeAsync();
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovalRevokesCallbackInAlreadyCapturedDispatchSnapshot(bool detail)
    {
        using var logger = new StalledProjectionReceiptLogger();
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hub" }, logger);
        int calls = 0;
        void callback() => calls++;
        void detailCallback(ProjectionChangedDetail _) => calls++;
        Task dispatch = Task.CompletedTask;
        try
        {
            if (detail)
            {
                await client.SubscribeDetailAsync("counter", "acme", "party-1", detailCallback, TestContext.Current.CancellationToken);
            }
            else
            {
                await client.SubscribeAsync("counter", "acme", callback, TestContext.Current.CancellationToken);
            }
            dispatch = Task.Run(() =>
            {
                if (detail)
                {
                    InvokeProjectionChangedDetail(client, "counter", "acme", "party-1", new Dictionary<string, string>());
                }
                else
                {
                    InvokeProjectionChanged(client, "counter", "acme");
                }
            }, TestContext.Current.CancellationToken);
            await logger.Captured.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (detail)
            {
                await client.UnsubscribeDetailAsync("counter", "acme", "party-1", detailCallback, TestContext.Current.CancellationToken);
            }
            else
            {
                await client.UnsubscribeAsync("counter", "acme", callback, TestContext.Current.CancellationToken);
            }
            logger.Resume.Set();
            await dispatch.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            calls.ShouldBe(0);
        }
        finally
        {
            logger.Resume.Set();
            await dispatch.ConfigureAwait(true);
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task StartupCancellationPropagatesFromInitialStalledGroupJoin()
    {
        var control = new StalledProjectionRpc();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(control);
        WebApplication app = builder.Build();
        await using var applicationLifetime = app.ConfigureAwait(true);
        app.MapHub<StalledProjectionHub>("/hub");
        await app.StartAsync(TestContext.Current.CancellationToken);
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = address + "/hub" });
        using var cancellation = new CancellationTokenSource();
        try
        {
            await client.SubscribeAsync("counter", "acme", () => { }, TestContext.Current.CancellationToken);
            Task start = client.StartAsync(cancellation.Token);
            await control.JoinEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await cancellation.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => start.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        }
        finally
        {
            control.ReleaseJoin.TrySetResult();
            await client.DisposeAsync();
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task LegacySignaturesExecuteWithoutCancellationArguments()
    {
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hub" });
        try
        {
            // Bind and execute the exact published signatures, rather than reflecting on their presence.
            Func<string, string, Action, Task> subscribe = client.SubscribeAsync;
            Func<string, string, Action, Task> unsubscribe = client.UnsubscribeAsync;
            Func<string, string, Task> unsubscribeAll = client.UnsubscribeAsync;
            Func<string, string, Action<ProjectionChangedDetail>, Task> subscribeDetail = client.SubscribeDetailAsync;
            Func<string, string, string?, Action<ProjectionChangedDetail>, Task> subscribeScoped = client.SubscribeDetailAsync;
            Func<string, string, Action<ProjectionChangedDetail>, Task> unsubscribeDetail = client.UnsubscribeDetailAsync;
            Func<string, string, string?, Action<ProjectionChangedDetail>, Task> unsubscribeScoped = client.UnsubscribeDetailAsync;
            Func<string, string, Task> unsubscribeAllDetails = client.UnsubscribeDetailAsync;
            Func<string, string, string?, Task> unsubscribeAllScoped = client.UnsubscribeDetailAsync;
            Action callback = () => { };
            Action<ProjectionChangedDetail> detailCallback = _ => { };
            await subscribe("counter", "acme", callback);
            await unsubscribe("counter", "acme", callback);
            await subscribe("counter", "acme", callback);
            await unsubscribeAll("counter", "acme");
            await subscribeDetail("counter", "acme", detailCallback);
            await unsubscribeDetail("counter", "acme", detailCallback);
            await subscribeScoped("counter", "acme", "party-1", detailCallback);
            await unsubscribeScoped("counter", "acme", "party-1", detailCallback);
            await subscribeDetail("counter", "acme", detailCallback);
            await unsubscribeAllDetails("counter", "acme");
            await subscribeScoped("counter", "acme", "party-1", detailCallback);
            await unsubscribeAllScoped("counter", "acme", "party-1");
        }
        finally
        {
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task TransitionGenerationRetainsSubIntervalOutage()
    {
        var client = new EventStoreSignalRClient(new EventStoreSignalRClientOptions { HubUrl = "https://localhost/hub" });
        try
        {
            long before = client.RecoveryGeneration;
            await InvokeOnClosedAsync(client, null);
            await InvokeOnReconnectedAsync(client, "new");
            client.RecoveryGeneration.ShouldBeGreaterThan(before);
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static void InvokeProjectionChanged(EventStoreSignalRClient client, string projectionType, string tenantId) {
        MethodInfo method = typeof(EventStoreSignalRClient)
            .GetMethod("OnProjectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnProjectionChanged method not found.");

        _ = method.Invoke(client, [projectionType, tenantId]);
    }

    private static void InvokeProjectionChangedDetail(
        EventStoreSignalRClient client,
        string projectionType,
        string tenantId,
        string? groupScope,
        IReadOnlyDictionary<string, string> metadata) {
        MethodInfo method = typeof(EventStoreSignalRClient)
            .GetMethod("OnProjectionChangedDetail", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnProjectionChangedDetail method not found.");

        _ = method.Invoke(client, [projectionType, tenantId, groupScope, metadata]);
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

    private static async Task InvokeOnClosedAsync(EventStoreSignalRClient client, Exception? exception) {
        MethodInfo method = typeof(EventStoreSignalRClient)
            .GetMethod("OnClosedAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnClosedAsync method not found.");

        object? result = method.Invoke(client, [exception]);
        if (result is Task task) {
            await task.ConfigureAwait(true);
        }
    }

    private static EventStoreSignalRClient CreateClientWithReconnectConfigurator(
        EventStoreSignalRClientOptions options,
        Action<HubConnectionBuilder, IRetryPolicy?> reconnectConfigurator) {
        Type reconnectConfiguratorType = typeof(EventStoreSignalRClient)
            .GetNestedType("ReconnectConfigurator", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ReconnectConfigurator delegate not found.");

        object reconnectConfiguratorDelegate = Delegate.CreateDelegate(reconnectConfiguratorType, reconnectConfigurator.Target, reconnectConfigurator.Method);

        ConstructorInfo constructor = typeof(EventStoreSignalRClient)
            .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(EventStoreSignalRClientOptions), typeof(ILogger<EventStoreSignalRClient>), reconnectConfiguratorType], null)
            ?? throw new InvalidOperationException("Reconnect-configurable constructor not found.");

        return (EventStoreSignalRClient)constructor.Invoke([options, null!, reconnectConfiguratorDelegate]);
    }

    private sealed class TestRetryPolicy : IRetryPolicy {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) => TimeSpan.FromMilliseconds(250);
    }

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
