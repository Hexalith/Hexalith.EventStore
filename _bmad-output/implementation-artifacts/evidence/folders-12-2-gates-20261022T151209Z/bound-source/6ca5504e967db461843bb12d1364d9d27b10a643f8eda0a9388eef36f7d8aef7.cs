namespace Hexalith.EventStore.Client.Events;

/// <summary>A support-safe failure to read a known event payload version.</summary>
public sealed class EventPayloadEvolutionException : Exception
{
    /// <summary>Creates a failure without retaining a payload or user exception.</summary>
    public EventPayloadEvolutionException(string eventTypeName, int storedVersion, long sequenceNumber,
        string reason, string? upcasterTypeName = null, string? innerExceptionTypeName = null)
        : base($"Event payload evolution failed for {eventTypeName} version {storedVersion} at sequence {sequenceNumber}: {reason}."
            + (upcasterTypeName is null ? string.Empty : $" Upcaster type: {upcasterTypeName}.")
            + (innerExceptionTypeName is null ? string.Empty : $" Inner exception type: {innerExceptionTypeName}."))
    {
        EventTypeName = eventTypeName;
        StoredVersion = storedVersion;
        SequenceNumber = sequenceNumber;
        Reason = reason;
        UpcasterTypeName = upcasterTypeName;
        InnerExceptionTypeName = innerExceptionTypeName;
    }

    /// <summary>Gets the stored event name.</summary>
    public string EventTypeName { get; }

    /// <summary>Gets the stored event version.</summary>
    public int StoredVersion { get; }

    /// <summary>Gets the stored event sequence.</summary>
    public long SequenceNumber { get; }

    /// <summary>Gets the support-safe failure reason without payload or original exception text.</summary>
    public string Reason { get; }

    /// <summary>Gets the upcaster type when a step failed.</summary>
    public string? UpcasterTypeName { get; }

    /// <summary>Gets only the type name of the original exception.</summary>
    public string? InnerExceptionTypeName { get; }
}
