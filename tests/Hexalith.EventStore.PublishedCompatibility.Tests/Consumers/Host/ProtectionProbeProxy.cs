using System.Reflection;
using Hexalith.EventStore.Server.Events;

namespace PublishedConsumer;

/// <summary>Counts actual protection calls while invoking the published unprotected provider.</summary>
public class ProtectionProbeProxy : DispatchProxy
{
    public int Calls { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? arguments)
    {
        Calls++;
        return method!.Invoke(new NoOpEventPayloadProtectionService(), arguments);
    }
}
