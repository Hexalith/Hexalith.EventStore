using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Candidate independently qualified current caller/root/writer and antirollback authority; no defaults or self-reporting proof.</summary>
public interface IInteractionOccurrenceAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates current private caller for exact occurrence and named Reserve, RetainSealed, CompleteWriter or Lookup method.</summary>
    Task<bool> AuthorizeOperationAsync(InteractionOccurrenceIdentity identity, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independent installed epoch and exact captured durable registry revision and state digest; stale or divergent restore cannot authorize lookup/encryption.</summary>
    Task<bool> ValidateStateAsync(string tenantId, string epochId, long revision, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Reads the independently installed initial epoch; missing installation disables the registry.</summary>
    Task<string?> GetInstalledEpochAsync(string tenantId, CancellationToken cancellationToken = default);
    /// <summary>Authenticates current exact root/alias/version, retained tenant DigestKey HMAC and writer occurrence authorization.</summary>
    Task<bool> AuthorizeReservationAsync(InteractionOccurrenceRequest request, CancellationToken cancellationToken = default);
    /// <summary>Authenticates actual complete v2 ciphertext/reference and exact current writer-completion lease, without accepting plaintext wrappers.</summary>
    Task<bool> VerifySealedAsync(InteractionOccurrenceRecord reservation, InteractionOccurrenceSealedResult sealedResult, CancellationToken cancellationToken = default);
    /// <summary>Authenticates exact durable source-write or authoritative never-written completion; unknown completion cannot activate or abort.</summary>
    Task<bool> VerifyWriterAsync(InteractionOccurrenceRecord record, string proofId, bool persisted, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordRevisionAsync(string tenantId, string epochId, long expectedRevision, long nextRevision, string exactStateDigest, CancellationToken cancellationToken = default);
}
