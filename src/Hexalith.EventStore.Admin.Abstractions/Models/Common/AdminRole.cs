namespace Hexalith.EventStore.Admin.Abstractions.Models.Common;

/// <summary>
/// Authorization roles for admin operations (NFR46).
/// </summary>
public enum AdminRole
{
    /// <summary>Developer-level read-only access: stream browsing, state inspection, type catalog, health dashboard.</summary>
    ReadOnly,

    /// <summary>DBA-level operator access: ReadOnly + projection controls, snapshot creation, compaction, dead-letter management.</summary>
    Operator,

    /// <summary>Infrastructure-level admin access: Operator + tenant management, backup/restore.</summary>
    Admin,
}
