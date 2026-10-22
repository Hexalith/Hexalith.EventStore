using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// User membership within a tenant.
/// </summary>
public sealed record TenantMember(string UserId, TenantRole Role);
