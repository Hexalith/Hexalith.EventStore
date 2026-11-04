using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Injects one ambiguous backend acknowledgement without changing real group behavior.</summary>
public sealed class ProjectionGroupFailure : IHubFilter
{
    private int _armed;
    /// <summary>Gets or sets whether the failure follows applied admission rather than preceding removal.</summary>
    public bool AddAfterApply { get; set; }

    /// <summary>Arms one backend operation failure.</summary>
    public void Arm() => Volatile.Write(ref _armed, 1);

    /// <summary>Consumes the selected target-group failure.</summary>
    public bool Fail(string group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.StartsWith("counter:", StringComparison.Ordinal) && Interlocked.Exchange(ref _armed, 0) == 1;
    }

    /// <inheritdoc/>
    public ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        context.Hub.Groups = new FaultingProjectionGroupManager(context.Hub.Groups, this);
        return next(context);
    }
}
