// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9.1, 10.3-10.4 and 15; amendment section 4.
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
