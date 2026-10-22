using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Full tenant details including members and configuration.
/// </summary>
public sealed record TenantDetail(
    string TenantId,
    string Name,
    string? Description,
    TenantStatus Status,
    IReadOnlyList<TenantMember> Members,
    IReadOnlyDictionary<string, string> Configuration,
    DateTimeOffset CreatedAt);
