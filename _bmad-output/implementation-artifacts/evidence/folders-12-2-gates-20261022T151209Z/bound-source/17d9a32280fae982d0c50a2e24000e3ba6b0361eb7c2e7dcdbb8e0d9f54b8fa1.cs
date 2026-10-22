namespace Hexalith.Tenants.Contracts.Enums;

/// <summary>
/// Tenant audit event category used for filtering operational evidence.
/// </summary>
public enum AuditEventCategory {
    /// <summary>
    /// Access and role management event.
    /// </summary>
    Access,

    /// <summary>
    /// Tenant administration and configuration event.
    /// </summary>
    Administrative,
}
