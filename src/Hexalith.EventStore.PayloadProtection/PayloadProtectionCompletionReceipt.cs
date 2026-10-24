// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9.1 and 10.3.
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
