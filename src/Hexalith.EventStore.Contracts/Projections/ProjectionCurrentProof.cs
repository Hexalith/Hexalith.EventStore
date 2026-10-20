using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>The existing guarded native source, selected immutable receipt and catalog observed current together.</summary>
/// <param name="Source">Actual native source evidence.</param>
/// <param name="Owner">The original selected generation and completed batch receipt.</param>
/// <param name="CatalogFingerprint">The verified route catalog.</param>
public sealed record ProjectionCurrentProof(NativeSourceProof Source, ProjectionOwnerProof Owner, string CatalogFingerprint);
