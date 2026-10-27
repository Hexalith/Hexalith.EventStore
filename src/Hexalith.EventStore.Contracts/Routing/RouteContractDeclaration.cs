using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Additive offline contract metadata; existing command/query interfaces remain unchanged.</summary>
/// <param name="RouteId">Stable unique identifier, independent of the deployment target.</param>
/// <param name="Kind">Exactly command, query, or projection.</param>
/// <param name="Domain">Owning domain.</param>
/// <param name="MessageType">Command/query discriminator, or null for projections. Asterisk requires a finite fallback list.</param>
/// <param name="ProjectionType">Projection discriminator; null for commands and explicitly computed v2 queries.</param>
/// <param name="ContractVersion">Explicit target contract version, such as v1.</param>
/// <param name="MessageIdVersion">Explicit command MessageId grammar (1 or 2); null for other kinds.</param>
/// <param name="CredentialKind">Credential for the target hop. Message/projection routes require workload.</param>
/// <param name="Operation">Exact target operation from the Contracts vocabulary.</param>
/// <param name="PlatformAdmission">Platform admission, or null for tenant-scoped declarations.</param>
/// <param name="FallbackTypes">Finite allowed discriminators for a fallback; empty for exact entries. Exact matches take precedence.</param>
/// <param name="Execution">Explicit schema v2 handler metadata; absent in schema v1.</param>
public sealed record RouteContractDeclaration(
    string RouteId,
    string Kind,
    string Domain,
    string? MessageType,
    string? ProjectionType,
    string ContractVersion,
    int? MessageIdVersion,
    string CredentialKind,
    string Operation,
    PlatformAdmissionDeclaration? PlatformAdmission,
    IReadOnlyList<string> FallbackTypes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RouteExecutionDeclaration? Execution = null);
