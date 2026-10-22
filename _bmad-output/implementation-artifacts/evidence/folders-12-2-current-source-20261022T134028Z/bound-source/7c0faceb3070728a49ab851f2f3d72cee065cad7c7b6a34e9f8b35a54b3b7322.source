using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>Crash-recoverable audited disposition awaiting source mutation and fresh coverage.</summary>
public sealed record ProcessPendingDisposition(ProcessSource Source, bool Applied, long PreparedGeneration, long AppliedGeneration)
{
    /// <summary>Exclusive recovery claim for this disposition generation.</summary>
    public string? ClaimToken { get; init; }

    /// <summary>Expiry of the exclusive recovery claim.</summary>
    public DateTimeOffset? ClaimUntil { get; init; }
}
