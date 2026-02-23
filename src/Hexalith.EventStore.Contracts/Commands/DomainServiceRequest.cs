
namespace Hexalith.EventStore.Contracts.Commands;
/// <summary>
/// DTO payload sent to the domain service via DAPR service invocation.
/// Lives in Contracts so both the invoker (Server) and the domain service (Client/Sample) share the same wire format.
/// </summary>
/// <param name="Command">The command envelope to process.</param>
/// <param name="CurrentState">The current aggregate state, or null for new aggregates.</param>
public record DomainServiceRequest(CommandEnvelope Command, object? CurrentState);
