namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Records whether malformed stored events reached Apply.</summary>
internal sealed class ConstructorFailureState
{
    /// <summary>Gets the number of applied events.</summary>
    public int Applied { get; private set; }

    /// <summary>Applies an event after successful deserialization.</summary>
    /// <param name="payload">The deserialized event.</param>
    public void Apply(ConstructorArgumentEvent payload) => Applied++;

    /// <summary>Applies an event after successful deserialization.</summary>
    /// <param name="payload">The deserialized event.</param>
    public void Apply(ConstructorInvalidOperationEvent payload) => Applied++;
}
