using System.Reflection;
using Hexalith.EventStore.Server.Events;

namespace PublishedConsumer;

/// <summary>Counts actual protection calls while invoking the published unprotected provider.</summary>
public class ProtectionProbeProxy : DispatchProxy
{
    /// <summary>Gets the number of actual published protection calls.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? method, object?[]? arguments)
    {
        Calls++;
        return method!.Invoke(new NoOpEventPayloadProtectionService(), arguments);
    }
}
