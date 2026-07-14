namespace Hexalith.EventStore.Server.Configuration;

/// <summary>Configures projection delivery-state retention and concurrency.</summary>
public sealed record ProjectionDeliveryIdempotencyOptions {
    /// <summary>The largest supported exact-receipt retention window.</summary>
    public const int MaximumCompletedReceiptLimit = 4096;

    /// <summary>Gets the maximum number of exact completed-event receipts retained per projection scope.</summary>
    public int CompletedReceiptLimit { get; init; } = 256;

    /// <summary>Gets the duration of a projection delivery reservation.</summary>
    public TimeSpan ReservationLease { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the maximum optimistic-concurrency attempts for one state transition.</summary>
    public int MaxStateTransitionAttempts { get; init; } = 8;

    /// <summary>Validates the frozen Story 1.13 bounds.</summary>
    public void Validate() {
        if (CompletedReceiptLimit is < 1 or > MaximumCompletedReceiptLimit) {
            throw new InvalidOperationException("Projection completed receipt limit must be between 1 and 4096.");
        }

        if (ReservationLease < TimeSpan.FromSeconds(30) || ReservationLease > TimeSpan.FromHours(24)) {
            throw new InvalidOperationException("Projection reservation lease must be between 30 seconds and 24 hours.");
        }

        if (MaxStateTransitionAttempts is < 1 or > 32) {
            throw new InvalidOperationException("Projection state transition attempts must be between 1 and 32.");
        }
    }
}
