using System.Text.Json;

using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>Durable multi-pass catalog and candidate coverage state shared by hosted replicas.</summary>
public sealed record ProcessSweepState(
    int Version,
    long Cycle,
    long Generation,
    bool Complete,
    DateTimeOffset? CycleStartedAt,
    DateTimeOffset? LastCompleteAt,
    string? CatalogAfterKey,
    bool CatalogComplete,
    int TenantPage,
    int TenantOffset,
    int CandidatePage,
    int CandidateOffset,
    bool CandidatesComplete,
    bool NextCatalog,
    int Unresolved,
    IReadOnlyDictionary<string, int> Reasons,
    IReadOnlyDictionary<string, ProcessPendingDisposition> Pending,
    string? Owner,
    DateTimeOffset? LeaseUntil,
    long Fence)
{
    /// <summary>Maximum serialized size of one durable sweep control record.</summary>
    public const int MaxSerializedBytes = 256 * 1024;

    /// <summary>Counted observations awaiting a durable per-source marker.</summary>
    public IReadOnlyDictionary<string, ProcessPendingObservation> PendingObservations { get; init; }
        = new Dictionary<string, ProcessPendingObservation>(StringComparer.Ordinal);

    /// <summary>Discoverable source identities whose multi-record publication has not completed.</summary>
    public IReadOnlyDictionary<string, ProcessSource> PendingPublications { get; init; }
        = new Dictionary<string, ProcessSource>(StringComparer.Ordinal);

    /// <summary>Source transitions that require an observation after their durable source write.</summary>
    public IReadOnlyDictionary<string, ProcessPendingTransition> PendingTransitions { get; init; }
        = new Dictionary<string, ProcessPendingTransition>(StringComparer.Ordinal);

    /// <summary>Last publication identity selected for repair.</summary>
    public string? PublicationRepairAfterKey { get; init; }

    /// <summary>Last disposition identity selected for repair.</summary>
    public string? DispositionRepairAfterKey { get; init; }

    /// <summary>Which repair queue receives the next available repair turn.</summary>
    public bool NextPublicationRepair { get; init; } = true;

    /// <summary>Next candidate page to inspect while repairing each pending publication.</summary>
    public IReadOnlyDictionary<string, int> PublicationCandidateRepairPages { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Next tenant page to inspect while repairing each pending publication.</summary>
    public IReadOnlyDictionary<string, int> PublicationTenantRepairPages { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Publications whose linked candidate page has unknown historical contents.</summary>
    public IReadOnlyDictionary<string, bool> PublicationCandidateUnknownPages { get; init; }
        = new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>Publications whose linked tenant page has unknown historical contents.</summary>
    public IReadOnlyDictionary<string, bool> PublicationTenantUnknownPages { get; init; }
        = new Dictionary<string, bool>(StringComparer.Ordinal);

    /// <summary>Whether the next tenant page is required by a persisted link.</summary>
    public bool ExpectedTenantPage { get; init; }

    /// <summary>Whether the next candidate page is required by a persisted link.</summary>
    public bool ExpectedCandidatePage { get; init; }

    /// <summary>Bounded sequential probes after a missing or unreadable tenant page.</summary>
    public int TenantProbeMisses { get; init; }

    /// <summary>Bounded sequential probes after a missing or unreadable candidate page.</summary>
    public int CandidateProbeMisses { get; init; }

    /// <summary>Monotonic number of distinct source identities accepted for durable retention.</summary>
    public long RetainedCandidates { get; init; }

    /// <summary>Whether retained population exceeds the configured coverage capacity.</summary>
    public bool CapacityExceeded { get; init; }

    /// <summary>Validates flags, cursors, timestamps, and pending evidence together.</summary>
    public void Validate(TimeProvider? time = null)
    {
        if (Version != 1 || Cycle < 0 || Generation < Cycle || Fence < 0 || Unresolved < 0 || RetainedCandidates < 0
            || TenantPage is < 0 or >= 100_000 || TenantOffset < 0 || CandidatePage is < 0 or >= 100_000 || CandidateOffset < 0
            || TenantProbeMisses is < 0 or > 1_000_000 || CandidateProbeMisses is < 0 or > 1_000_000
            || Reasons is null || Pending is null || PendingObservations is null || PendingPublications is null || PendingTransitions is null
            || PublicationCandidateRepairPages is null || PublicationTenantRepairPages is null
            || PublicationCandidateUnknownPages is null || PublicationTenantUnknownPages is null
            || Reasons.Count > 16 || Pending.Count > 1024 || PendingObservations.Count > 4096 || PendingPublications.Count > 128 || PendingTransitions.Count > 1024
            || Reasons.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)
            || Reasons.Sum(static pair => (long)pair.Value) != Unresolved
            || Pending.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null
                || pair.Value.Source is null || pair.Key != ProcessStateKeys.SourceId(pair.Value.Source)
                || pair.Value.PreparedGeneration < 1 || pair.Value.PreparedGeneration > Generation
                || pair.Value.AppliedGeneration < 0 || pair.Value.AppliedGeneration > Generation
                || pair.Value.Applied && pair.Value.AppliedGeneration < pair.Value.PreparedGeneration
                || pair.Value.Applied != (pair.Value.AppliedGeneration > 0)
                || (pair.Value.ClaimToken is null) != (pair.Value.ClaimUntil is null)
                || pair.Value.Applied && pair.Value.ClaimToken is not null)
            || PendingObservations.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null
                || pair.Value.Source is null || pair.Key != ProcessStateKeys.SourceId(pair.Value.Source)
                || pair.Value.Cycle != Cycle)
            || PendingPublications.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null
                || pair.Key != ProcessStateKeys.SourceId(pair.Value))
            || PendingTransitions.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null
                || pair.Value.Source is null || pair.Key != ProcessStateKeys.SourceId(pair.Value.Source)
                || pair.Value.Generation < 1 || pair.Value.Generation > Generation || string.IsNullOrWhiteSpace(pair.Value.Token))
            || PublicationCandidateRepairPages.Any(pair => !PendingPublications.ContainsKey(pair.Key) || pair.Value is < 0 or >= 1_000_000)
            || PublicationTenantRepairPages.Any(pair => !PendingPublications.ContainsKey(pair.Key) || pair.Value is < 0 or >= 1_000_000)
            || PublicationCandidateUnknownPages.Any(pair => !PendingPublications.ContainsKey(pair.Key) || !pair.Value)
            || PublicationTenantUnknownPages.Any(pair => !PendingPublications.ContainsKey(pair.Key) || !pair.Value)
            || Complete && (PendingPublications.Count != 0 || PendingTransitions.Count != 0)
            || Complete && PendingObservations.Count != 0
            || (Owner is null) != (LeaseUntil is null)
            || Owner is { Length: 0 }
            || Cycle == 0 && CycleStartedAt is not null
            || Cycle == 0 && (Generation != 0 || Unresolved != 0 || Reasons.Count != 0 || Pending.Count != 0 || Complete || PendingObservations.Count != 0 || PendingPublications.Count != 0 || PendingTransitions.Count != 0)
            || Cycle > 0 && CycleStartedAt is null
            || LastCompleteAt is not null && CycleStartedAt is null
            || Complete && (!CatalogComplete || !CandidatesComplete || Pending.Count != 0
                || CatalogAfterKey is not null || TenantPage != 0 || TenantOffset != 0 || CandidatePage != 0 || CandidateOffset != 0
                || ExpectedTenantPage || ExpectedCandidatePage || TenantProbeMisses != 0 || CandidateProbeMisses != 0
                || LastCompleteAt is null || CycleStartedAt > LastCompleteAt)
            || CandidatesComplete && (TenantPage != 0 || TenantOffset != 0 || CandidatePage != 0 || CandidateOffset != 0
                || ExpectedTenantPage || ExpectedCandidatePage || TenantProbeMisses != 0 || CandidateProbeMisses != 0)
            || SerializedSize() > MaxSerializedBytes)
        {
            throw new InvalidOperationException("Process sweep state is invalid.");
        }
    }

    /// <summary>Returns the complete serialized control-record size, including all pending maps.</summary>
    public int SerializedSize() => JsonSerializer.SerializeToUtf8Bytes(this).Length;
}
