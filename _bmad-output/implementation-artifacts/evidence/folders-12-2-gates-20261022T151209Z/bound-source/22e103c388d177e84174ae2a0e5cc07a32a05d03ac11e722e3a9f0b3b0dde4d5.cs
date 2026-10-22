using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>Persisted technical discovery state; poison is sticky until a separately authorized repair exists.</summary>
/// <param name="Scope">The installed exact namespace/feed.</param>
/// <param name="Revision">The conditional state revision.</param>
/// <param name="AuthorityRevision">The last authenticated finite cut revision.</param>
/// <param name="Sources">The reconciled per-source committed prefix vector.</param>
/// <param name="Entries">The retained immutable ordered publication references.</param>
/// <param name="PoisonCode">A closed safe conflict category that prevents checkpoint release.</param>
public sealed record SourcePublicationIndexState(SourcePublicationScope Scope, long Revision, string AuthorityRevision, IReadOnlyList<SourcePublicationHead> Sources, IReadOnlyList<SourcePublicationIndexEntry> Entries, string? PoisonCode = null)
{
    /// <summary>Independently retained exact source-prefix work; partial work cannot release a checkpoint.</summary>
    public SourcePublicationReconciliationProgress? ReconciliationProgress { get; init; }
    /// <summary>Independent durable acknowledgement-prefix proof; stale binding/cut remains retained but cannot authorize resume.</summary>
    public SourcePublicationDispatchProgress? DispatchProgress { get; init; }
}

