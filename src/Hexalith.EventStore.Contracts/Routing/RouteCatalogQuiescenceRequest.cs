namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Owner-verifiable completion evidence for one durable fence; client completion and elapsed time are insufficient.</summary>
/// <param name="LeaseId">Exact persisted dispatch identity.</param>
/// <param name="RootDigest">Exact generation root at acquisition.</param>
/// <param name="Epoch">Exact activation or rollback epoch at acquisition.</param>
/// <param name="Proof">Opaque owner-issued proof of remote quiescence.</param>
public sealed record RouteCatalogQuiescenceRequest(string LeaseId, string RootDigest, long Epoch, string Proof);
