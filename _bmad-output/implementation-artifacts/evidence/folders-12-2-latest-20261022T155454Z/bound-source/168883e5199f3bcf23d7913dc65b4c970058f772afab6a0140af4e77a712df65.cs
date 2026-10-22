namespace Hexalith.Tenants.Contracts.Projections;

/// <summary>
/// Shared format for the ordered aggregate-sequence projection-version marker stamped by
/// <c>TenantProjectionHandler</c> and interpreted by <c>TenantMembershipCommandProvenance</c>.
/// </summary>
/// <remarks>
/// Not a wire contract (command/event/query/enum) -- it is an internal read-model format detail that
/// happens to need cross-assembly visibility, so it is deliberately kept out of the
/// <c>Hexalith.Tenants.Contracts.Queries</c> namespace the event-contract-reference governance test sweeps.
/// </remarks>
public static class TenantProjectionVersionFormat
{
    /// <summary>
    /// Prefix that precedes the aggregate-local, monotonically increasing EventStore sequence number.
    /// A projection version without this prefix is a legacy or store-specific opaque marker.
    /// </summary>
    public const string SequencePrefix = "tenant-sequence:";
}
