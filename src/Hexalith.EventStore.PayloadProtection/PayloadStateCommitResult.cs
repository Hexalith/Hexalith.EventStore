// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10; amendment section 4.
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
