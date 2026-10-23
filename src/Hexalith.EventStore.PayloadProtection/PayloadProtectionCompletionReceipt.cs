// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9.1 and 10.3.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Call-scoped issued-context evidence, never persisted or reconstructed from untrusted completion input.</summary>
internal sealed class PayloadProtectionCompletionReceipt
{
    /// <summary>Gets the serialization gate for idempotent completion of this exact issued context.</summary>
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    /// <summary>Gets or sets the monotonic beginning of a confirmed completion lease.</summary>
    internal long? LeaseStarted { get; set; }
    /// <summary>Gets or sets a terminal actor persistence outcome accepted by this issued call.</summary>
    internal PayloadProtectionPersistenceOutcome? CompletedOutcome { get; set; }
}
