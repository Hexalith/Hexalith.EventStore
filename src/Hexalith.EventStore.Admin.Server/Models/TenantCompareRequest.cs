using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Server.Models;

/// <summary>
/// Request body for tenant usage comparison.
/// </summary>
/// <param name="TenantIds">The tenant identifiers to compare.</param>
public record TenantCompareRequest(
    [property: Required] IReadOnlyList<string> TenantIds);
