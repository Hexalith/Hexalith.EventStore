
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
    /// <summary>Ephemeral effect origin supplied only by the admitted target over authenticated workload transport.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Hexalith.EventStore.Contracts.Effects.TrustedEffectOrigin? TrustedEffectOrigin { get; init; }

    /// <summary>Requests optional post-command folded state for a snapshot-due invocation.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludePostCommandState { get; init; }

    /// <summary>Gets the deployment-owned capture qualification selected for this request.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SnapshotCaptureProfileId { get; init; }

    /// <summary>Gets the negotiated event writer mode, when the request is version-aware.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WriterMode { get; init; }

    /// <summary>Gets the active event registry fingerprint, when the request is version-aware.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RegistryFingerprint { get; init; }

    /// <summary>Gets signed proof for the verified effective command state.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? CommandStateProof { get; init; }

    /// <summary>Gets the ordered verified effective command events, when supplied.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VerifiedEffectiveCommandEvent>? VerifiedEffectiveEvents { get; init; }

    /// <summary>Short-lived signed gateway proof of the human caller; never part of the command envelope or persisted state.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? HumanAuthorityReceipt { get; init; }
}
