namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Status of a tenant.
/// </summary>
public enum TenantStatusType
{
    /// <summary>The tenant is active and operational.</summary>
    Active,

    /// <summary>The tenant has been suspended.</summary>
    Suspended,

    /// <summary>The tenant is being onboarded.</summary>
    Onboarding,
}
