namespace Hexalith.EventStore.Client.Projections;

/// <summary>Durable promotion and catch-up evidence for one tenant/family epoch.</summary>
public sealed record SharedProjectionEpochStatus(
    long Epoch,
    long ActiveGeneration,
    string? OperationId,
    string? StageFingerprint,
    bool IsBuilding,
    bool IsAborting,
    int PendingDeliveryCount,
    bool IsOffboarded = false)
{
    /// <summary>The tenant identity of the session retained by a global projection scope.</summary>
    public string? OperationTenantId { get; init; }
}
