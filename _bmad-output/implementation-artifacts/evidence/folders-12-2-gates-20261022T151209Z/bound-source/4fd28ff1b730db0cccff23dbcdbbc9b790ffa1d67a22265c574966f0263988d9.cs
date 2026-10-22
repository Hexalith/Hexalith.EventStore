namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Declares the current schema version of a JSON domain event payload.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventPayloadVersionAttribute(int version) : Attribute
{
    /// <summary>Gets the declared version, from 1 through 1024.</summary>
    public int Version { get; } = version;
}
