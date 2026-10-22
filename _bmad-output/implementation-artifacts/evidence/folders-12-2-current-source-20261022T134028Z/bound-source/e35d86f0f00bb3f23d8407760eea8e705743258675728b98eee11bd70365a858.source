namespace Hexalith.EventStore.DomainService;

/// <summary>Durable disposition of one source stream.</summary>
public enum ProcessSourceStatus
{
    /// <summary>Ready to read new envelopes.</summary>
    Active,
    /// <summary>A bounded read left more source envelopes.</summary>
    Incomplete,
    /// <summary>A retryable failure is retained.</summary>
    Retrying,
    /// <summary>The hot retry budget is exhausted.</summary>
    Parked,
    /// <summary>Invalid or conflicting evidence is retained.</summary>
    Quarantined,
    /// <summary>An audited disposition requires the same validator to run again.</summary>
    Revalidating,
}
