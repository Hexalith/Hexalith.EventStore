using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Provides a separately declared payload fixture for bounded producer verification.</summary>
internal sealed record BoundedProducerTestEvent : IEventPayload;
