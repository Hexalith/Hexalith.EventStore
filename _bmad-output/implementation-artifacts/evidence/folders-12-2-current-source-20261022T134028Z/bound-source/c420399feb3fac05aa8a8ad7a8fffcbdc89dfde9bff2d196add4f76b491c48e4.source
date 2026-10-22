namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>Requests current owner evidence for the exact bound requester and ChatBot operation.</summary>
public sealed record ResolveTenantOwnerAuthority(
    string TenantId, string PrincipalId, string Operation, string ActorClass, string Origin)
{
    public static string QueryType => typeof(ResolveTenantOwnerAuthority).FullName!;
}

/// <summary>Metadata-only evidence from a complete current tenant source stream.</summary>
public sealed record TenantOwnerAuthorityResult(
    string TenantId, string PrincipalId, string Operation, string ActorClass, string Origin,
    bool Allowed, long SourceVersion, string ObservationId, DateTimeOffset ObservedAt);
