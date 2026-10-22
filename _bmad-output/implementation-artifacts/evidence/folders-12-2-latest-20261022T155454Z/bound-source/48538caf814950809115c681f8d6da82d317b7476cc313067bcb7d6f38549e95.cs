using Hexalith.EventStore.Client.TestContracts;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Applies events from the separate fixture assembly.</summary>
internal sealed class ExternalEventState
{
    /// <summary>Gets the accumulated value.</summary>
    public int Value { get; private set; }

    /// <summary>Applies an external event.</summary>
    /// <param name="payload">The event to apply.</param>
    public void Apply(ExternalVersionedEvent payload) => Value += payload.Value;
}
