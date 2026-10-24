// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10; amendment section 4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Distinguishes confirmed atomic persistence from an ambiguous request.</summary>
internal enum PayloadStateCommitResult
{
    /// <summary>The exact transaction was acknowledged.</summary>
    Committed = 1,
    /// <summary>The component rejected an optimistic concurrency condition.</summary>
    Conflict = 2,
    /// <summary>Persistence may have occurred; durable operation state must be reconciled.</summary>
    Unknown = 3,
    /// <summary>The state service denied authorization.</summary>
    Denied = 4,
}
