using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Test-only trusted persistence acknowledgement; the Server integration remains owned by Story 8.7.</summary>
internal sealed class LifecycleQualificationEvidence(string operationId) : PayloadPersistenceEvidenceSource
{
    /// <inheritdoc/>
    internal override ValueTask<PayloadProtectionPersistenceOutcome> ObserveAsync(AggregateIdentity identity, OperationV1 operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        operation.ScopeDigest.ShouldBe(PayloadProtectionStateKeys.ScopeDigest(identity));
        return ValueTask.FromResult(operation.OperationId == operationId ? PayloadProtectionPersistenceOutcome.Persisted : PayloadProtectionPersistenceOutcome.Unknown);
    }
}
