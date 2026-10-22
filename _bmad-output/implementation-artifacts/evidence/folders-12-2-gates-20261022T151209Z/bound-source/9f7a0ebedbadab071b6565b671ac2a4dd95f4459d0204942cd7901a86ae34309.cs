namespace Hexalith.EventStore.Server.Actors;

/// <summary>Describes governed tenant-scoped consumed-key retention.</summary>
public enum IdempotencyTenantLifecycleState
{
    /// <summary>The managed tenant is active and consumed evidence cannot be purged.</summary>
    Active,

    /// <summary>An approved deletion workflow started the 400-day retention countdown.</summary>
    Retaining,

    /// <summary>A legal hold paused the remaining retention interval.</summary>
    LegalHold,

    /// <summary>The governed interval elapsed and bounded purge may proceed.</summary>
    PurgeEligible,

    /// <summary>Every registered protected admission and directory alias was purged.</summary>
    Purged,
}
