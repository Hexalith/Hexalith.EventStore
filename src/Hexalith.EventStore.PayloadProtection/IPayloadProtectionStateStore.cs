// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10; amendment sections 2-4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The strongly consistent, qualified atomic state primitive consumed by lifecycle coordinators.</summary>
internal interface IPayloadProtectionStateStore
{
    /// <summary>Reads an exact state key, validating the closed schema and response ETag.</summary>
    ValueTask<PayloadProtectionStateValue<T>?> ReadAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    /// <summary>Submits one transaction once; ambiguous outcomes must be reconciled, never blindly replayed.</summary>
    ValueTask<PayloadStateCommitResult> CommitAsync(IReadOnlyList<PayloadProtectionStateMutation> mutations, CancellationToken cancellationToken = default);
}
