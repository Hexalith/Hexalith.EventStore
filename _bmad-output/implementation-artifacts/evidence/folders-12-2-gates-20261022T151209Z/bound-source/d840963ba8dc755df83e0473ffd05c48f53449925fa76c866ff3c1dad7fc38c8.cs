namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Transfers one retry-safe bounded part of a mapped full replay request.</summary>
/// <param name="Header">The source-admitted request identity and transport reference; its event array is empty.</param>
/// <param name="Ordinal">Zero-based part ordinal.</param>
/// <param name="Data">One bounded slice of the serialized complete request.</param>
public sealed record ProjectionRebuildTransportPartRequest(
    ProjectionDispatchRequest Header,
    int Ordinal,
    byte[] Data);
