namespace Hexalith.EventStore.Admin.Server.Authorization;

/// <summary>
/// Admin-specific JWT claim types.
/// </summary>
public static class AdminClaimTypes {
    /// <summary>Claim containing the user's admin role (ReadOnly, Operator, Admin).</summary>
    public const string AdminRole = "eventstore:admin-role";

    /// <summary>Tenant claim type used for multi-tenant authorization.</summary>
    public const string Tenant = "eventstore:tenant";
}
