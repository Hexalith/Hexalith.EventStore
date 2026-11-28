namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Unique durable lease identity makes a lost CAS response distinguishable from another concurrent dispatch.</summary>
/// <param name="Sequence">Monotonic authority sequence.</param>
/// <param name="LeaseId">Platform-generated ULID, retained across CAS retries.</param>
/// <param name="HostId">Exact acquiring host.</param>
/// <param name="RootDigest">Exact committed root at acquisition.</param>
/// <param name="Epoch">Exact committed epoch at acquisition.</param>
public sealed record RouteCatalogDispatchFence(long Sequence, string LeaseId, string HostId, string RootDigest, long Epoch)
{
    /// <summary>Gets whether a shared generation fence has been sealed against new admissions.
    /// Null denotes an independent dispatch fence.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? SharedAdmissionSealed { get; init; }
}
