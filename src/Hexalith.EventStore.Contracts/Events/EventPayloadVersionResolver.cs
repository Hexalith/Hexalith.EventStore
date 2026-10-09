namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Reads and validates an event type's declared payload version.</summary>
public static class EventPayloadVersionResolver
{
    /// <summary>Gets the declared version, treating an absent attribute as version one.</summary>
    public static int GetDeclaredVersion(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        int version = Attribute.GetCustomAttribute(eventType, typeof(EventPayloadVersionAttribute)) is EventPayloadVersionAttribute declaration
            ? declaration.Version
            : 1;
        if (version is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(eventType), eventType.FullName,
                $"Event payload type {eventType.FullName} declares invalid version {version}; expected 1 through 1024.");
        }

        return version;
    }
}
