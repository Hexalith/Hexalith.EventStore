using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Supplies explicit test-only persistence evidence; no event-store scan is treated as proof.</summary>
internal sealed class LifecyclePersistenceEvidence(PayloadProtectionPersistenceOutcome outcome) : PayloadPersistenceEvidenceSource
{
    /// <summary>Gets or sets a claim-race hook executed at the evidence boundary.</summary>
    internal Func<OperationV1, CancellationToken, ValueTask>? Observed { get; set; }
    /// <summary>Returns the existing persistence outcome only for the exact operation requested by recovery.</summary>
    internal override async ValueTask<PayloadProtectionPersistenceOutcome> ObserveAsync(AggregateIdentity identity, OperationV1 operation, CancellationToken cancellationToken)
    {
        operation.ScopeDigest.ShouldBe(PayloadProtectionStateKeys.ScopeDigest(identity));
        if (Observed is not null) { await Observed(operation, cancellationToken); }
        return outcome;
    }
}
