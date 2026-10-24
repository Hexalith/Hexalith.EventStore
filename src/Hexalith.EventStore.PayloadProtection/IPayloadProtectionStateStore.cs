// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10; amendment sections 2-4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The strongly consistent, qualified atomic state primitive consumed by lifecycle coordinators.</summary>
internal interface IPayloadProtectionStateStore
{
    /// <summary>Reads an exact state key, validating the closed schema and response ETag.</summary>
    ValueTask<PayloadProtectionStateValue<T>?> ReadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    /// <summary>Submits one transaction once; ambiguous outcomes must be reconciled, never blindly replayed.</summary>
    ValueTask<PayloadStateCommitResult> CommitAsync(IReadOnlyList<PayloadProtectionStateMutation> mutations, CancellationToken cancellationToken = default);
}
