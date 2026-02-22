
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.DomainServices;
/// <summary>
/// DTO payload sent to the domain service via DAPR service invocation.
/// </summary>
/// <param name="Command">The command envelope to process.</param>
/// <param name="CurrentState">The current aggregate state, or null for new aggregates.</param>
public record DomainServiceRequest(CommandEnvelope Command, object? CurrentState);
