extern alias BlazorUI;

using Shouldly;

using CounterRefreshLifetime = BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services.CounterRefreshLifetime;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>Exercises renderer refresh coalescing and the component's cancellation lifetime.</summary>
public sealed class CounterRefreshLifetimeTests
{
    [Fact]
    public async Task RefreshesDuringLoad_CoalesceAfterTheRunningRequest()
    {
        using var lifetime = new CounterRefreshLifetime();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();

        Task running = lifetime.RunAsync(async _ =>
        {
            calls.Add("first");
            await release.Task;
            calls.Add("completed");
        });
        await lifetime.RunAsync(_ => throw new InvalidOperationException("The newer refresh supersedes this one."));
        await lifetime.RunAsync(_ =>
        {
            calls.Add("latest");
            return Task.CompletedTask;
        });

        calls.ShouldBe(["first"]);
        release.SetResult();
        await running;

        calls.ShouldBe(["first", "completed", "latest"]);
    }

    [Fact]
    public async Task Dispose_CancelsCurrentRequestAndDiscardsQueuedAndFutureRefreshes()
    {
        using var lifetime = new CounterRefreshLifetime();
        CancellationToken observed = default;
        Task running = lifetime.RunAsync(token =>
        {
            observed = token;
            return Task.Delay(Timeout.InfiniteTimeSpan, token);
        });

        await lifetime.RunAsync(_ => throw new InvalidOperationException("Queued work must be discarded."));
        lifetime.Dispose();
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        await lifetime.RunAsync(_ => throw new InvalidOperationException("Disposed components must reject new work."));

        lifetime.IsDisposed.ShouldBeTrue();
        observed.IsCancellationRequested.ShouldBeTrue();
    }
}
