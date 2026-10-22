using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

/// <summary>
/// Represents an event payload used to verify <see cref="Hexalith.EventStore.Testing.Fakes.FakeEventPersister"/> behavior.
/// </summary>
/// <param name="Name">The test event name.</param>
internal sealed record FakeEventPersisterTestEvent(string Name) : IEventPayload;
