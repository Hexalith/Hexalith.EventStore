using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Sharding.TestHost;

using Shouldly;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Verifies actual transport cancellation, draining and bounded diagnostic streams.</summary>
public sealed class TransportLifecycleTests
{
    /// <summary>JSON-safe records reach HTTP, and fencing cancels and drains that actual request.</summary>
    [Fact]
    public async Task ActorJsonRequestUsesCancelableUnderlyingTransportAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var reached = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new RunCancellationHandler(cancellation.Token, new CallbackHttpHandler(async (request, token) =>
        {
            request.Headers.Contains("dapr-api-token").ShouldBeFalse();
            reached.SetResult(await request.Content!.ReadAsStringAsync(token));
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("An uncanceled transport unexpectedly completed.");
        }));
        IShardActor proxy = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = "http://127.0.0.1:1", UseJsonSerialization = true, DaprApiToken = "inherited-token-sentinel",
            RequestTimeout = TimeSpan.FromSeconds(5),
        }, handler).CreateActorProxy<IShardActor>(new ActorId(ShardIdentity.Encode("tenant-sentinel", "domain")), ShardActor.ActorType);
        var tracker = new RunActivityTracker();
        Task<ShardReply> actual = tracker.Start(() => proxy.ApplyAsync(new("Reserve", "tenant-sentinel", "domain", 1, 3)));
        string serialized = await reached.Task.WaitAsync(TimeSpan.FromSeconds(3));
        serialized.ShouldContain("tenant-sentinel");
        Task[] outstanding = tracker.Fence();
        outstanding.Length.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => tracker.Start(() => Task.FromResult(1)));
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await actual);
        outstanding.All(task => task.IsCompleted).ShouldBeTrue();
    }

    /// <summary>Headers do not end the run deadline while the response body is still stalled.</summary>
    [Fact]
    public async Task RunCancellationStopsStalledBodyAfterHeadersAsync()
    {
        using var cancellation = new CancellationTokenSource();
        using var body = new StallingResponseStream();
        using var handler = new RunCancellationHandler(cancellation.Token, new CallbackHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(body) })));
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        Task<HttpResponseMessage> pending = client.GetAsync("http://127.0.0.1:1/stalled-body");
        await body.Reading.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(2)));
        pending.IsCompleted.ShouldBeTrue();
        body.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Small command output remains exact while large streams retain a bounded tail.</summary>
    [Fact]
    public async Task CommandCapturePreservesExactSmallOutputAndBoundsLargeOutputAsync()
    {
        using var small = new StringReader("sha256:0123456789\n");
        (await BoundedCommandCapture.ReadAsync(small, 128, CancellationToken.None)).ShouldBe("sha256:0123456789\n");
        using var large = new StringReader(new string('x', 100_000) + "last-line\n");
        string result = await BoundedCommandCapture.ReadAsync(large, 128, CancellationToken.None);
        result.Length.ShouldBeLessThanOrEqualTo(140);
        result.ShouldStartWith("<truncated>\n");
        result.ShouldEndWith("last-line\n");
    }
}
