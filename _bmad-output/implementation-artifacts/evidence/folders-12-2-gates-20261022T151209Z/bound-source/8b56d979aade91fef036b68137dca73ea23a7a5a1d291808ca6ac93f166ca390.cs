namespace Hexalith.EventStore.Client.Streams;

/// <summary>Independent private exact operation/lookup credentials and authoritative immutable permit/command classification/retained DigestKey intent checks.</summary>
/// <remarks>Each operation borrows the captured request Payload read-only until its returned Task actually terminates,
/// including after caller cancellation or timeout. Do not mutate, retain or share this array after termination.
/// Any independently qualified retained representation must own its own copy and lifetime; the client clears its copy
/// after termination of all started operations.</remarks>
public interface IDirectoryAtomicAppendAuthority
{
    /// <summary>Authenticates exact current caller/method and original command/target; verifies source membership from owner facts, not claimed classification or source fields.</summary>
    Task<bool> AuthorizeAsync(DirectoryAtomicAppendRequest ownedRequest, string method, string requestDigest, CancellationToken cancellationToken = default);
    /// <summary>Authenticates original durable atomic transaction proof and exact writer attribution; provider self-report/command acceptance alone is insufficient.</summary>
    Task<bool> VerifyOutcomeAsync(DirectoryAtomicAppendRequest ownedRequest, string requestDigest, DirectoryAtomicAppendOutcome outcome, CancellationToken cancellationToken = default);
}
