namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Immutable technical transport identity, separate from command/query eligibility.</summary>
/// <param name="Owner">Exact SDK controller type.</param>
/// <param name="Verbs">Canonical space-separated HTTP verbs independently observed at this endpoint.</param>
/// <param name="CodeDigest">SHA256 of the actual loaded SDK owner assembly.</param>
public sealed record RouteCatalogHttpTransport(string Owner, string Verbs, string CodeDigest)
{
    /// <summary>Gets the exact independently observed ACL path/verb witnesses; they confer no code or message authority.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RouteCatalogHttpGrant>? Grants { get; init; }
}
