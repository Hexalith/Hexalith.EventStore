namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Opaque, metadata-only detail for a projection-changed broadcast.
/// <para>
/// Values in <see cref="Metadata"/> are treated as metadata by the framework and MUST NOT
/// carry authoritative content (no response text, chunks, prompts, or domain payloads).
/// Domain meaning lives in the consuming application; the framework stays content-agnostic
/// so the metadata-only floor is enforceable at the framework boundary.
/// </para>
/// <para>
/// <see cref="GroupScope"/>, when present, narrows the SignalR group below tenant level
/// (for example, a conversation id), producing the group name
/// <c>{ProjectionType}:{TenantId}:{GroupScope}</c>. When <see langword="null"/> or empty,
/// the broadcast targets the tenant-wide group <c>{ProjectionType}:{TenantId}</c>.
/// </para>
/// </summary>
/// <param name="ProjectionType">The projection type name (kebab-case).</param>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="GroupScope">
/// Optional sub-tenant group scope (for example, a conversation id). Null or empty targets the
/// tenant-wide group; a non-empty value targets the scoped group.
/// </param>
/// <param name="Metadata">
/// Opaque, bounded metadata key/value pairs. Never authoritative content. Bounds (key count and
/// total byte size) are enforced by the broadcaster at the framework boundary.
/// </param>
public sealed record ProjectionChangedDetail(
    string ProjectionType,
    string TenantId,
    string? GroupScope,
    IReadOnlyDictionary<string, string> Metadata);
