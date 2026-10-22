namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independent exact conditional-transition journal, separate from the primary owner store and private caller authority.</summary>
/// <remarks>The installed authority binds the complete owner/purpose scope, predecessor revision/digest and target revision/digest.
/// Record atomically compares the current predecessor anchor, advances it and retains this immutable transition proof.
/// Exact retry reuses the original proof. Verification is a fresh independent read; neither pending bytes nor cached state are authority.</remarks>
public interface IAnchoredStateTransitionAuthority
{
    /// <summary>Independently authenticates current permission for this exact original operation and durably retains its bounded transition before primary staging.</summary>
    /// <remarks>Admission does not advance the anchor. The qualified owner verifies original ownership/intent, scope, predecessor, target and bytes; store-supplied bytes alone cannot create permission. Exact retries retain the original independent admission.</remarks>
    Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Task.FromResult(false);
    /// <summary>Conditionally resolves only an independently retained exact admitted original under current recovery permission and the unchanged predecessor anchor.</summary>
    /// <remarks>This separately authorized mutation is not read verification. Missing admission, withdrawn original permission, differing bytes/scope or Unknown denies without advancement. It retains the immutable exact journal outcome; no original caller is required, and no physical effect is repeated.</remarks>
    Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Task.FromResult(false);
    /// <summary>Atomically records the exact transition and advances its independently governed current anchor. Missing implementation denies.</summary>
    Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Task.FromResult(false);
    /// <summary>Authenticates the exact original transition, including its predecessor, target and scope; it does not advance an anchor.</summary>
    Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
