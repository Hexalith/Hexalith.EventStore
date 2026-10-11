using Hexalith.EventStore.Client.Processes;

namespace Hexalith.EventStore.DomainService;

/// <summary>Versioned, CAS-owned checkpoint and unresolved evidence for one source.</summary>
public sealed record ProcessSourceState(
    int Version,
    ProcessSource Source,
    long Checkpoint,
    ProcessSourceStatus Status,
    int Attempts,
    long? FailureSequence,
    string? ReasonCode,
    string? Owner,
    DateTimeOffset? LeaseUntil,
    long Fence,
    long ObservedCycle,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Whether this source already contributed an unresolved reason in its observed cycle.</summary>
    public bool ObservedUnresolved { get; init; }

    /// <summary>Last disposition generation durably applied to this source.</summary>
    public long LastDispositionGeneration { get; init; }

    /// <summary>Last sweep generation prepared before a source transition.</summary>
    public long LastTransitionGeneration { get; init; }

    /// <summary>Exact durable handoff token for the latest source transition.</summary>
    public string? LastTransitionToken { get; init; }

    /// <summary>Validates a persisted source record before use.</summary>
    public void Validate(ProcessSource source)
    {
        Source.Validate();
        if (Version != 1 || Source != source || Checkpoint < 0 || Attempts < 0 || Fence < 0 || ObservedCycle < 0 || LastDispositionGeneration < 0 || LastTransitionGeneration < 0
            || !Enum.IsDefined(Status) || FailureSequence is < 1 || ObservedCycle == 0 && ObservedUnresolved
            || (Owner is null) != (LeaseUntil is null)
            || (Status is ProcessSourceStatus.Parked or ProcessSourceStatus.Quarantined or ProcessSourceStatus.Retrying) != (ReasonCode is not null)
            || LastTransitionToken is { Length: 0 }
            || UpdatedAt == default)
        {
            throw new InvalidOperationException("Process source state is invalid.");
        }
    }
}
