namespace Hexalith.EventStore.Client.Streams;

/// <summary>Required one-owner atomic persistence/writer contract. No implementation is inferred from an actor metadata read plus a later AggregateActor SaveStateAsync.</summary>
/// <remarks>Each operation borrows the captured request Payload read-only until its returned Task actually terminates,
/// including after caller cancellation or timeout. Do not mutate, retain or share this array after termination.
/// Any independently qualified retained representation must own its own copy and lifetime; the client clears its copy
/// after termination of all started operations.</remarks>
public interface IAtomicDirectoryAppendOwner
{
    /// <summary>At the target write's single durable linearization, enforce current installed epoch, exact authoritative permit/effect/membership and original finite bridge,
    /// current admission/content fences and closed command category; persist writer-assigned ordinal/high-water with target events in the SAME transaction.
    /// Cross-owner read/check then write implementations are forbidden. Missing qualified transaction/writer integration must return Unavailable.</summary>
    Task<DirectoryAtomicAppendOutcome> TryAppendAsync(DirectoryAtomicAppendRequest ownedRequest, string requestDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticated exact original outcome lookup; never re-authorizes or repeats an unresolved physical append.</summary>
    Task<DirectoryAtomicAppendOutcome> LookupAsync(DirectoryAtomicAppendRequest ownedRequest, string requestDigest, CancellationToken cancellationToken = default);
}
