
namespace Hexalith.EventStore.Contracts.Commands;
/// <summary>
/// DTO payload sent to the domain service via DAPR service invocation.
/// Lives in Contracts so both the invoker (Server) and the domain service (Client/Sample) share the same wire format.
/// </summary>
/// <param name="Command">The command envelope to process.</param>
/// <param name="CurrentState">
/// The current aggregate state payload, or null for new aggregates.
/// This may be a typed state instance, a JSON-serialized state object, or a <see cref="DomainServiceCurrentState"/>
/// that carries snapshot state plus tail events for snapshot-aware rehydration.
/// </param>
public record DomainServiceRequest(CommandEnvelope Command, object? CurrentState) {
    /// <summary>Gets the negotiated event writer mode, when the request is version-aware.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WriterMode { get; init; }

    /// <summary>Gets the active event registry fingerprint, when the request is version-aware.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistryFingerprint { get; init; }
}
