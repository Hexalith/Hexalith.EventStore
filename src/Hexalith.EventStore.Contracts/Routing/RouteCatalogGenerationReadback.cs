using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Protected readback of the shared persisted authority, never a process-local readiness claim.</summary>
/// <param name="Epoch">Monotonic commit/rollback fence.</param>
/// <param name="Generation">Committed envelope generation.</param>
/// <param name="RootDigest">Committed root.</param>
/// <param name="RouteFacetDigest">Committed routing facet.</param>
/// <param name="IdempotencyFacetDigest">Committed admission facet.</param>
/// <param name="Profile">Committed profile.</param>
/// <param name="RequiredHosts">Exact committed host set.</param>
/// <param name="Serving">Whether dispatch can acquire a lease.</param>
public sealed record RouteCatalogGenerationReadback(long Epoch, long Generation, string RootDigest,
    string RouteFacetDigest, string IdempotencyFacetDigest, string Profile,
    IReadOnlyList<RouteCatalogHostIdentity> RequiredHosts, bool Serving)
{
    /// <summary>Gets the actual committed attempt for an opt-in complete v2 activation; absent for legacy readbacks.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttemptId { get; init; }

    /// <summary>Gets the credential-free complete v2 binding, including exact peer observations and technical membership.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActivationBindingDigest { get; init; }
}
