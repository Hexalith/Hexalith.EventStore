using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>The canonical source stream whose actor identity also keys the coordination actor.</summary>
/// <param name="Source">The authoritative source aggregate identity.</param>
/// <param name="RequiresSourceValidation">Whether to validate its persisted events before target dispatch.</param>
public sealed record CoordinatedCommandScope(AggregateIdentity Source, bool RequiresSourceValidation);
