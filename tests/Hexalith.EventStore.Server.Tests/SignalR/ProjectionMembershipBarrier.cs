using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Reorders real RPC execution or loses its acknowledgement without changing the production hub.</summary>
public sealed class ProjectionMembershipBarrier : IHubFilter
{
    private int _armed;
    private bool _after;
    private readonly ConcurrentDictionary<long, TaskCompletionSource> _applied = new();

    /// <summary>Gets the actual server connection identity, including clients that skip negotiation.</summary>
    public string ConnectionId { get; private set; } = string.Empty;

    /// <summary>Gets the held invocation entry.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets the explicit invocation release.</summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets completion of the held invocation.</summary>
    public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Holds the next snapshot before application or after application before acknowledgement.</summary>
    public void Arm(bool after) { _after = after; Volatile.Write(ref _armed, 1); }

    /// <summary>Waits for an actually executed acknowledgement generation.</summary>
    public Task Applied(long generation) => _applied.GetOrAdd(generation, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        ConnectionId = context.Hub.Context.ConnectionId;
        if (context.HubMethodName != "ReconcileProjectionMembership") { return await next(context); }
        bool hold = Interlocked.Exchange(ref _armed, 0) == 1;
        if (hold && !_after) { Entered.TrySetResult(); await Release.Task; }
        object? result = await next(context);
        if (result is long generation) { _applied.GetOrAdd(generation, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(); }
        if (hold && _after) { Entered.TrySetResult(); await Release.Task; }
        if (hold) { Finished.TrySetResult(); }
        return result;
    }
}
