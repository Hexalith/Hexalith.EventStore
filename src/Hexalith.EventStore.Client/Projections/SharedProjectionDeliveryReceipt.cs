namespace Hexalith.EventStore.Client.Projections;

internal sealed record SharedProjectionDeliveryReceipt(
    string Digest,
    bool Parked = false,
    SharedProjectionFailureStatus? Failure = null);
