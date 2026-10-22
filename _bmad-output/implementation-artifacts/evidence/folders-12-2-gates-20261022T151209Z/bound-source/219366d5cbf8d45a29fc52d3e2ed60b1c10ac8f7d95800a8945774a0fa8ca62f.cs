namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Identifies the malformed component of an event identity without exposing payload data.</summary>
public sealed class EventIdentityValidationException : ArgumentException
{
    /// <summary>Creates a support-safe identity failure.</summary>
    public EventIdentityValidationException(string componentName)
        : base($"Invalid event identity component: {componentName}.", componentName)
    {
        ComponentName = componentName;
    }

    /// <summary>Gets the rejected component name.</summary>
    public string ComponentName { get; }
}
