using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Actors;

internal sealed record CommandRecoveryEvent : IEventPayload;
