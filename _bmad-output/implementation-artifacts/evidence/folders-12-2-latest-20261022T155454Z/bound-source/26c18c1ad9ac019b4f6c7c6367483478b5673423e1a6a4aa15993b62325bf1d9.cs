using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>An exact reconciled finite-cut checkpoint, not proof of absence beyond its source vector.</summary>
/// <param name="Scope">The exact namespace installation.</param>
/// <param name="IndexRevision">The persisted index revision.</param>
/// <param name="AuthorityRevision">The independently authenticated namespace cut revision.</param>
/// <param name="Sources">The exact source-prefix coverage vector.</param>
/// <param name="LastOffset">The last discovered publication offset.</param>
public sealed record SourcePublicationCheckpoint(SourcePublicationScope Scope, long IndexRevision, string AuthorityRevision, IReadOnlyList<SourcePublicationHead> Sources, long LastOffset);
