// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9.1, 10.3-10.4 and 15; amendment section 4.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Host-owned recovery evidence from the existing trusted event/snapshot persistence boundary; integration belongs to Story 8.7.</summary>
internal abstract class PayloadPersistenceEvidenceSource
{
    /// <summary>Confirms the exact operation's persisted key reference or save absence. Incomplete scans, expiry and quarantine must return Unknown.</summary>
    internal abstract ValueTask<PayloadProtectionPersistenceOutcome> ObserveAsync(AggregateIdentity identity,
        OperationV1 operation, CancellationToken cancellationToken);
}
