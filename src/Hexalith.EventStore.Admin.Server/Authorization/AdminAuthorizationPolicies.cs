namespace Hexalith.EventStore.Admin.Server.Authorization;

/// <summary>
/// Defines authorization policy names for admin API endpoints (NFR46).
/// </summary>
public static class AdminAuthorizationPolicies
{
    /// <summary>ReadOnly: stream browsing, state inspection, type catalog, health.</summary>
    public const string ReadOnly = "AdminReadOnly";

    /// <summary>Operator: ReadOnly + projection controls, snapshots, compaction, dead-letters.</summary>
    public const string Operator = "AdminOperator";

    /// <summary>Admin: Operator + tenant management, backup/restore.</summary>
    public const string Admin = "AdminFull";
}
