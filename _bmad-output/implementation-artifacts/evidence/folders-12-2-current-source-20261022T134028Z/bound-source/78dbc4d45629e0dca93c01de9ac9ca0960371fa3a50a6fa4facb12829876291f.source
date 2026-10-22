using System.Text.Json.Serialization;

namespace Hexalith.Tenants.Contracts.Enums;

/// <summary>
/// Tenant membership role. <see cref="Unknown"/> (ordinal 0) is a non-privileged sentinel:
/// a missing or defaulted role deserializes here and is rejected by the aggregate
/// (MeetsMinimumRole default-denies it). Serialized by name so a missing or unrecognized
/// value fails closed rather than mapping to a privileged role (TEN-1).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantRole>))]
public enum TenantRole {
    Unknown = 0,
    TenantOwner,
    TenantContributor,
    TenantReader,
}
