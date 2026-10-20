namespace Hexalith.EventStore.Server.Projections;

/// <summary>Classifies ownership of a guarded probe without authorizing cleanup.</summary>
internal sealed record ProjectionSourceGuardDeliveryResult(bool Owned, bool AlreadyCurrent, string? ReasonCode)
{
    /// <summary>Retains only the original content-free owner proofs established by current inspection.</summary>
    internal IReadOnlyDictionary<string, Hexalith.EventStore.Contracts.Projections.ProjectionOwnerProof> ProvenOwners { get; init; }
        = new Dictionary<string, Hexalith.EventStore.Contracts.Projections.ProjectionOwnerProof>();

    /// <summary>Retains the exact native proof whose prefix was compared to the completion binding.</summary>
    internal Hexalith.EventStore.Server.Events.SourceProof? ProvenSource { get; init; }

    /// <summary>Indicates that exact conditional completion was read back, independently of newer source/lifecycle freshness.</summary>
    internal bool CompletionProven { get; init; }

    /// <summary>Proves that this tail already completed its original replay-manifest cleanup.</summary>
    internal bool FinalizedCleanupProven { get; init; }

    /// <summary>Gets the most recent source head actually sampled during finalization.</summary>
    internal long? FinalSourceHeadSequence { get; init; }

    /// <summary>Retains the monotonic start of this attempt's finalization across worker cleanup.</summary>
    internal long? FinalizationStartedTimestamp { get; init; }

    /// <summary>Creates only the remaining independent cleanup time, or a fresh budget for a later Current activation.</summary>
    internal CancellationTokenSource CreateCleanupBudget(TimeProvider clock)
    {
        TimeSpan remaining = TimeSpan.FromSeconds(30) - (FinalizationStartedTimestamp is { } started
            ? clock.GetElapsedTime(started) : TimeSpan.Zero);
        if (remaining > TimeSpan.Zero) return new(remaining, clock);
        var expired = new CancellationTokenSource();
        expired.Cancel();
        return expired;
    }

    /// <summary>Identifies the observed proven head for separately scheduled, identity-bound cleanup.</summary>
    internal long? ObservedHeadSequence { get; init; }

    /// <summary>Identifies the original persisted head, never a generated cleanup identity.</summary>
    internal string? ObservedHeadMessageId { get; init; }

    /// <summary>Binds cleanup to the verified route generation inspected by this attempt.</summary>
    internal string? CatalogFingerprint { get; init; }

    /// <summary>Contains only selected persisted freshness versions proven at the observed source/Idle binding.</summary>
    internal IReadOnlyDictionary<string, string> ProvenFreshnessVersions { get; init; } = new Dictionary<string, string>();
}
