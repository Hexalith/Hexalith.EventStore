namespace Hexalith.EventStore.DomainService;

/// <summary>Operational limits for the synthetic checkpointed process runtime.</summary>
public sealed class EventStoreProcessOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "EventStore:Processes";

    /// <summary>Longest lease <see cref="Validate"/> accepts.</summary>
    internal static readonly TimeSpan MaximumLeaseDuration = TimeSpan.FromMinutes(10);

    /// <summary>Dapr state component for process control records.</summary>
    public string StateStoreName { get; set; } = "statestore";

    /// <summary>Maximum envelopes requested in one stream page.</summary>
    public int PageSize { get; set; } = 32;

    /// <summary>Maximum envelopes serviced before yielding to another source.</summary>
    public int MaxEnvelopesPerRun { get; set; } = 8;

    /// <summary>Maximum serialized bytes in one envelope.</summary>
    public int MaxEventBytes { get; set; } = 64 * 1024;

    /// <summary>Maximum serialized source bytes in one page.</summary>
    public int MaxPageBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Maximum effects from one envelope.</summary>
    public int MaxEffectsPerEnvelope { get; set; } = 4;

    /// <summary>Maximum copied command bytes from one envelope.</summary>
    public int MaxCommandBytesPerEnvelope { get; set; } = 256 * 1024;

    /// <summary>Maximum copied command bytes from one page.</summary>
    public int MaxCommandBytesPerPage { get; set; } = 1024 * 1024;

    /// <summary>Maximum catalog or index entries requested at once.</summary>
    public int DiscoveryPageSize { get; set; } = 64;

    /// <summary>Maximum source observations in one reconciliation pass.</summary>
    public int SourcesPerPass { get; set; } = 1024;

    /// <summary>Maximum retained candidate population covered by the configured freshness budget.</summary>
    public int SupportedCandidateCount { get; set; } = 6;

    /// <summary>Maximum hot attempts before parking a source.</summary>
    public int RetryLimit { get; set; } = 3;

    /// <summary>Maximum CAS attempts for control records.</summary>
    public int WriteAttempts { get; set; } = 8;

    /// <summary>Maximum wall time for an external read, translation, or submission.</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Wall-time service budget for one source; work yields after a durable envelope checkpoint.</summary>
    public TimeSpan SourceServiceQuantum { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>Duration of a source processing lease.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Time between hosted recovery passes.</summary>
    public TimeSpan ReconciliationInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum age of a completed cycle before readiness degrades.</summary>
    public TimeSpan ReadinessMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Whether the periodic hosted reconciler runs.</summary>
    public bool ReconciliationEnabled { get; set; } = true;

    /// <summary>Returns validation failures; valid values have practical memory and time ceilings.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(StateStoreName)) errors.Add("StateStoreName is required.");
        if (PageSize is < 1 or > 256) errors.Add("PageSize must be 1..256.");
        if (MaxEnvelopesPerRun is < 1 or > 64) errors.Add("MaxEnvelopesPerRun must be 1..64.");
        if (MaxEventBytes is < 1 or > 1024 * 1024) errors.Add("MaxEventBytes must be 1..1048576.");
        if (MaxPageBytes is < 1 or > 16 * 1024 * 1024 || MaxPageBytes < MaxEventBytes) errors.Add("MaxPageBytes must fit MaxEventBytes and be at most 16 MiB.");
        if (MaxEffectsPerEnvelope is < 1 or > 256) errors.Add("MaxEffectsPerEnvelope must be 1..256.");
        if (MaxCommandBytesPerEnvelope is < 1 or > 1024 * 1024) errors.Add("MaxCommandBytesPerEnvelope must be 1..1048576.");
        if (MaxCommandBytesPerPage is < 1 or > 8 * 1024 * 1024 || MaxCommandBytesPerPage < MaxCommandBytesPerEnvelope) errors.Add("MaxCommandBytesPerPage must fit one envelope and be at most 8 MiB.");
        if (DiscoveryPageSize is < 1 or > 256) errors.Add("DiscoveryPageSize must be 1..256.");
        if (SourcesPerPass is < 1 or > 4096) errors.Add("SourcesPerPass must be 1..4096.");
        if (SupportedCandidateCount is < 1 or > 1_000_000) errors.Add("SupportedCandidateCount must be 1..1000000.");
        if (RetryLimit is < 1 or > 20) errors.Add("RetryLimit must be 1..20.");
        if (WriteAttempts is < 1 or > 16) errors.Add("WriteAttempts must be 1..16.");
        if (OperationTimeout < TimeSpan.FromMilliseconds(100) || OperationTimeout > TimeSpan.FromMinutes(2)) errors.Add("OperationTimeout must be 100 ms..2 minutes.");
        if (LeaseDuration < OperationTimeout * 3 || LeaseDuration > MaximumLeaseDuration)
            errors.Add("LeaseDuration must allow three operation deadlines and be at most 10 minutes.");
        if (SourceServiceQuantum < TimeSpan.FromMilliseconds(100) || SourceServiceQuantum > TimeSpan.FromMinutes(10))
            errors.Add("SourceServiceQuantum must be 100 ms..10 minutes.");
        if (OperationTimeout > TimeSpan.Zero && MaxEffectsPerEnvelope > 0
            && SourceServiceQuantum.Ticks < (long)(4 + 5 * MaxEffectsPerEnvelope) * OperationTimeout.Ticks)
            errors.Add("SourceServiceQuantum must fit one envelope's translation, effects, and checkpoint deadlines.");
        if (ReconciliationInterval < TimeSpan.FromSeconds(1) || ReconciliationInterval > TimeSpan.FromMinutes(5)) errors.Add("ReconciliationInterval must be 1 second..5 minutes.");
        if (ReadinessMaxAge <= ReconciliationInterval || ReadinessMaxAge > TimeSpan.FromMinutes(30)) errors.Add("ReadinessMaxAge must exceed the interval and be at most 30 minutes.");
        if (ReadinessMaxAge <= LeaseDuration + ReconciliationInterval)
            errors.Add("ReadinessMaxAge must outlast the sweep lease and one reconciliation interval.");
        if (ReconciliationInterval > TimeSpan.Zero && ReadinessMaxAge > ReconciliationInterval
            && SupportedCandidateCount > 0 && SourcesPerPass > 0 && DiscoveryPageSize > 0)
        {
            // Reserve a second source quantum for envelope-boundary progress,
            // sixteen operation deadlines per retained source for one fair repair
            // unit. Four work units per source cover catalog, candidates, and repair.
            long sources = SupportedCandidateCount;
            long workSteps = checked(4 * sources);
            long passes = (workSteps + SourcesPerPass - 1) / SourcesPerPass;
            // Catalog pages are not retained across passes. At SourcesPerPass=1,
            // every source requires its own request, irrespective of page size.
            long catalogPages = sources;
            decimal requiredTicks = sources * (2m * SourceServiceQuantum.Ticks + 16m * OperationTimeout.Ticks)
                + catalogPages * (decimal)OperationTimeout.Ticks + passes * (decimal)ReconciliationInterval.Ticks;
            if (requiredTicks > ReadinessMaxAge.Ticks - ReconciliationInterval.Ticks)
                errors.Add("SupportedCandidateCount cannot fit bounded source service, catalog and candidate reads, scan passes, and freshness slack.");
        }
        return errors;
    }
}
