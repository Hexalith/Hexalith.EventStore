using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>Content-free current Read evidence from the native canonical Tenant prefix.</summary>
/// <param name="TenantId">The exact managed Tenant, independent of the internal system source scope.</param>
/// <param name="PrincipalId">The SDK-verified actor.</param>
/// <param name="Allowed">The current existing Reader, Contributor or Owner Read policy decision.</param>
/// <param name="NativeProof">The actual native prefix folded for this decision.</param>
/// <param name="VerifiedAt">The final native source verification instant.</param>
public sealed record TenantReadAuthorityResult(string TenantId, string PrincipalId, bool Allowed,
    NativeSourceProof NativeProof, DateTimeOffset VerifiedAt);
