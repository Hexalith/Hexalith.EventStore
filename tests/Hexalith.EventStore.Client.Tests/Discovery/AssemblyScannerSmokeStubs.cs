
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.TestContracts;

namespace Hexalith.EventStore.Client.Tests.Discovery;

/// <summary>Public aggregate stub for smoke tests via <c>GetExportedTypes()</c>.</summary>
public sealed class SmokeTestAggregate : EventStoreAggregate<SmokeTestState> { }

/// <summary>State type for <see cref="SmokeTestAggregate"/>.</summary>
public sealed class SmokeTestState { }

/// <summary>Public projection stub for smoke tests via <c>GetExportedTypes()</c>.</summary>
public sealed class SmokeTestProjection : EventStoreProjection<SmokeTestReadModel> { }

/// <summary>Read model type for <see cref="SmokeTestProjection"/>.</summary>
public sealed class SmokeTestReadModel {
    /// <summary>Gets the accumulated external event value.</summary>
    public int Value { get; private set; }

    /// <summary>Applies an event whose upcaster lives in a separate contracts assembly.</summary>
    /// <param name="payload">The event to apply.</param>
    public void Apply(ExternalVersionedEvent payload) {
        ArgumentNullException.ThrowIfNull(payload);
        Value += payload.Value;
    }
}
