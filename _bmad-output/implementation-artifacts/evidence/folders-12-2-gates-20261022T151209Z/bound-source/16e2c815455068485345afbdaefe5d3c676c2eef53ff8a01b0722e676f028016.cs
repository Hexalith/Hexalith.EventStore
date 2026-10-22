namespace Hexalith.EventStore.Server.Tests.DomainServices;

/// <summary>Applies the effective event payloads after rehydration.</summary>
internal sealed class VersionedActorState
{
    /// <summary>Gets the accumulated value.</summary>
    public int Value { get; private set; }

    /// <summary>Applies a current schema event.</summary>
    public void Apply(VersionedActorEvent @event) => Value += @event.Value;
}
