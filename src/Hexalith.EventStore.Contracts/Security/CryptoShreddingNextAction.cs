namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — stable operator next-action hints. The kebab-case names are the wire contract;
/// localized prose may accompany the code but is never the contract.
/// </summary>
public enum CryptoShreddingNextAction {
    /// <summary>No further action is required.</summary>
    None = 0,

    /// <summary>Register the missing protection key with the configured provider.</summary>
    RegisterKey = 1,

    /// <summary>Submit an explicit operator decision (accept, block, or quarantine).</summary>
    SubmitOperatorDecision = 2,

    /// <summary>Provide additional restore evidence (manifest, provenance, or attestation).</summary>
    ProvideRestoreEvidence = 3,

    /// <summary>Retry the operation after the provider becomes available again.</summary>
    RetryWithBackoff = 4,

    /// <summary>Investigate the data provenance (likely consistency or bytes/metadata mismatch).</summary>
    InvestigateProvenance = 5,

    /// <summary>Upgrade EventStore to support an unknown metadata version.</summary>
    UpgradeEventStore = 6,
}
