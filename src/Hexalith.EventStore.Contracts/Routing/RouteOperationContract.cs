using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owner-declared serialized operation shapes and surface eligibility, independently digested from routing declarations.</summary>
/// <param name="RouteId">The stable executable declaration join.</param>
/// <param name="ContractVersion">The serialized operation version.</param>
/// <param name="Ingress">Exactly public or internal. Internal operations remain workload-only.</param>
/// <param name="Eligibility">Exactly agent-eligible, UI-only, confirmation-required or step-up.</param>
/// <param name="RequestSchema">Value-free request JSON schema produced using the owner's serializer.</param>
/// <param name="ResultSchema">Value-free result JSON schema produced using the owner's serializer.</param>
/// <param name="Digest">Schema-three operation contract digest, covering every preceding field.</param>
/// <param name="Kind">The declared command, query or projection identity.</param>
/// <param name="Domain">The owner domain identity.</param>
/// <param name="MessageType">The exact declared wire operation or projection discriminator.</param>
public sealed record RouteOperationContract(string RouteId, string ContractVersion, string Ingress, string Eligibility,
    JsonElement RequestSchema, JsonElement ResultSchema, string Digest, string? Kind = null, string? Domain = null, string? MessageType = null);
