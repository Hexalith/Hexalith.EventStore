using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;

namespace Hexalith.EventStore.Admin.Server.Models;

/// <summary>
/// Request body for triggering a consistency check.
/// </summary>
/// <param name="TenantId">Optional tenant scope (null for all tenants).</param>
/// <param name="Domain">Optional domain scope (null for all domains).</param>
/// <param name="CheckTypes">Types of consistency checks to perform.</param>
public record ConsistencyCheckRequest(
    string? TenantId,
    string? Domain,
    IReadOnlyList<ConsistencyCheckType> CheckTypes);
