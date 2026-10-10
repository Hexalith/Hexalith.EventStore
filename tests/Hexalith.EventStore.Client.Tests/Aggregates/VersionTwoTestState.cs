using Hexalith.EventStore.Client.Tests.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>State rebuilt from current version-two events.</summary>
internal sealed class VersionTwoTestState
{
    /// <summary>Gets the accumulated value.</summary>
    public int Value { get; private set; }

    /// <summary>Applies one current event.</summary>
    public void Apply(VersionTwoTestEvent @event) => Value += @event.Value;
}
