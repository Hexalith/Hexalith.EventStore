namespace Hexalith.EventStore.Client.Projections;

/// <summary>Reports whether the requested key is safe to reload after bounded recovery.</summary>
/// <param name="IsResolved">True when no pending envelope remains, including a compensated optimistic conflict.</param>
/// <param name="Reason">A closed metadata-only failure reason; no stored payload or identity is exposed.</param>
/// <param name="WasRecovered">True when this call reconciled an original pending batch.</param>
public sealed record ReadModelPendingBatchRecoveryResult(bool IsResolved, string? Reason = null, bool WasRecovered = false)
{
    /// <summary>Validated immutable retained retirement used only to derive the next private SDK attempt.</summary>
    internal string? RetiredAttemptFingerprint { get; init; }
}
