namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Fresh authoritative body lifecycle state, never a projection or cache result.</summary>
/// <param name="Generation">Monotonic generation changing on every resume, reference or hold transition.</param>
/// <param name="Terminal">Work is terminal, including durable abandonment for never-assigned uploads.</param>
/// <param name="HasActiveTask">Any task still owns active work.</param>
/// <param name="HasLiveReference">Any staged or committed authoritative reference remains.</param>
/// <param name="LegalHold">Physical preservation is required.</param>
public sealed record ContentDisposition(long Generation, bool Terminal, bool HasActiveTask, bool HasLiveReference, bool LegalHold)
{
    /// <summary>Whether a fresh seven-day epoch may run.</summary>
    public bool Eligible => Terminal && !HasActiveTask && !HasLiveReference && !LegalHold;
}
