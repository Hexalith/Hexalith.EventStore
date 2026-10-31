using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>An installed SDK technical MVC action, independent of executable domain ownership and eligibility.</summary>
/// <param name="Pattern">Actual route template.</param>
/// <param name="Methods">Actual HTTP methods.</param>
/// <param name="ActionIdentity">Loaded SDK assembly, module and method identity.</param>
/// <param name="RequestDigest">Actual bound parameter types, sources and serializer schemas.</param>
/// <param name="ResponseDigest">Actual declared response types and serializer schemas.</param>
/// <param name="Policies">Installed named human/admin policies.</param>
/// <param name="PolicyDigest">Effective credential and claim requirements.</param>
/// <param name="FilterDigest">Installed MVC filter identities.</param>
public sealed record RouteCatalogTechnicalHttpEndpoint(string Pattern, IReadOnlyList<string> Methods,
    string ActionIdentity, string RequestDigest, string ResponseDigest, IReadOnlyList<string> Policies,
    string PolicyDigest, string FilterDigest)
{
    /// <summary>Gets the preserved dedicated SDK workload credential kind; omitted for unchanged human/admin descriptors.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CredentialKind { get; init; }
}
