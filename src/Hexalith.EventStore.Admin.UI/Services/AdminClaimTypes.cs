namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Defines claim type constants for Admin JWT tokens.
/// Single source of truth for claim names shared between Server and UI.
/// </summary>
public static class AdminClaimTypes
{
    /// <summary>
    /// The claim type for the admin role (ReadOnly, Operator, Admin).
    /// </summary>
    public const string Role = "eventstore:admin-role";
}
