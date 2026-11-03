using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Controls negotiation, failed recovery probes and an invocation held beyond heartbeats.</summary>
public sealed class ProjectionMembershipRecoveryControl : IHubFilter
{
    private int _stalled;
    private int _snapshotFailure;
    private int _probeFailure;
    private int _membershipAttempts;
    private int _transientFailures;
    private int _replacementFailures;
    private string? _stalledConnection;
    private readonly System.Collections.Concurrent.ConcurrentQueue<long> _attemptTimes = new();

    /// <summary>Gets the number of actual membership mutations attempted.</summary>
    public int MembershipAttempts => Volatile.Read(ref _membershipAttempts);
    /// <summary>Gets the monotonic membership-attempt timestamps.</summary>
    public long[] AttemptTimes => _attemptTimes.ToArray();
    /// <summary>Gets or sets transient failures remaining; a negative value fails until cleared.</summary>
    public int TransientFailures { get => Volatile.Read(ref _transientFailures); set => Volatile.Write(ref _transientFailures, value); }
    /// <summary>Gets or sets transient failures injected only after replacement startup.</summary>
    public int ReplacementFailures { get => Volatile.Read(ref _replacementFailures); set => Volatile.Write(ref _replacementFailures, value); }
    /// <summary>Gets or sets whether to hold the replacement connection's membership acknowledgement.</summary>
    public bool HoldReplacementAcknowledgement { get; set; }
    /// <summary>Gets entry into the replacement acknowledgement gap.</summary>
    public TaskCompletionSource ReplacementApplied { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets release of the replacement acknowledgement.</summary>
    public TaskCompletionSource ReplacementRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Resets the successful-mutation witness.</summary>
    public void ResetRecoveryWitness() => Recovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets or sets whether the peer exposes only legacy membership RPCs.</summary>
    public bool Legacy { get; set; }
    /// <summary>Gets or sets whether the next mutation remains held until explicit release.</summary>
    public bool Stall { get; set; }
    /// <summary>Gets or sets the server identity of the current physical connection.</summary>
    public string ConnectionId { get; private set; } = string.Empty;
    /// <summary>Gets entry into the deliberately stalled mutation.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets the release for the original mutation.</summary>
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Gets a completed recovery snapshot or legacy join.</summary>
    public TaskCompletionSource Recovered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Arms a new stalled mutation and its subsequent recovery witness.</summary>
    public void ArmStall() { Recovered = new(TaskCreationOptions.RunContinuationsAsynchronously); Stall = true; }
    /// <summary>Injects one rejected snapshot followed by one unavailable authorization probe.</summary>
    public void FailRecoveryProbe() { _snapshotFailure = 1; _probeFailure = 1; }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        ConnectionId = context.Hub.Context.ConnectionId;
        bool membership = context.HubMethodName is "ReconcileProjectionMembership" or "JoinGroup" or "JoinGroupScoped" or "LeaveGroup" or "LeaveGroupScoped";
        if (membership)
        {
            Interlocked.Increment(ref _membershipAttempts);
            _attemptTimes.Enqueue(System.Diagnostics.Stopwatch.GetTimestamp());
            if (_stalledConnection is not null && context.Hub.Context.ConnectionId != _stalledConnection
                && Volatile.Read(ref _replacementFailures) > 0)
            {
                Interlocked.Decrement(ref _replacementFailures);
                throw new HubException("Temporary replacement membership backend unavailable.");
            }
            int failures = Volatile.Read(ref _transientFailures);
            if (failures != 0)
            {
                if (failures > 0) { Interlocked.Decrement(ref _transientFailures); }
                throw new HubException("Temporary membership backend unavailable.");
            }
        }
        if (Legacy && context.HubMethodName == "GetProjectionMembershipProtocol") { throw new HubException("Unknown hub method."); }
        if (context.HubMethodName == "ReconcileProjectionMembership" && Interlocked.Exchange(ref _snapshotFailure, 0) == 1)
        { throw new HubException("Tenant authorization failed."); }
        if (context.HubMethodName == "ValidateProjectionGroup" && Interlocked.Exchange(ref _probeFailure, 0) == 1)
        { throw new HubException("Tenant authorization unavailable."); }
        bool mutation = context.HubMethodName is "ReconcileProjectionMembership" or "LeaveGroup" or "LeaveGroupScoped";
        if (Stall && mutation && Interlocked.CompareExchange(ref _stalled, 1, 0) == 0)
        {
            _stalledConnection = context.Hub.Context.ConnectionId;
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
        }
        object? result = await next(context).ConfigureAwait(false);
        if (HoldReplacementAcknowledgement && membership && _stalledConnection is not null && context.Hub.Context.ConnectionId != _stalledConnection)
        {
            ReplacementApplied.TrySetResult();
            await ReplacementRelease.Task.ConfigureAwait(false);
        }
        if ((Stall && Volatile.Read(ref _stalled) == 1 && context.HubMethodName is "JoinGroup" or "JoinGroupScoped" or "ReconcileProjectionMembership") || (_probeFailure == 0 && context.HubMethodName == "ReconcileProjectionMembership"))
        { Recovered.TrySetResult(); }
        return result;
    }
}
