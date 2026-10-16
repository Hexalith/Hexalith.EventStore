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
    /// <summary>Durable conflicting delivery identities and their explicit repair audit.</summary>
    public IReadOnlyList<SharedProjectionDeliveryConflict> DeliveryConflicts { get; init; } = [];

    /// <summary>The first identity exceeding the bounded evidence capacity; requires scope restore or offboarding.</summary>
    public SharedProjectionDeliveryConflict? DeliveryConflictOverflow { get; init; }

    /// <summary>Discoverable terminal source dispositions, independent of successful domain membership.</summary>
    public IReadOnlyList<SharedProjectionParkingEvidence> ParkingEvidence { get; init; } = [];

    /// <summary>The first parked identity exceeding bounded evidence capacity.</summary>
    public SharedProjectionParkingEvidence? ParkingEvidenceOverflow { get; init; }

    /// <summary>Failed pending folds whose accepted source has not completed application.</summary>
    public IReadOnlyList<SharedProjectionFailureStatus> PendingFoldFailures { get; init; } = [];

    /// <summary>Whether legacy state has consumed the space reserved for emergency conflict evidence.</summary>
    public bool IsCapacityLimited { get; init; }

    /// <summary>Whether accepted contents remain readable but unresolved identity evidence prevents current readiness.</summary>
    public bool IsDegraded => PendingFoldFailures.Count > 0 || ParkingEvidenceOverflow is not null
        || ParkingEvidence.Any(item => item.ResolvedByOperationId is null) || IsCapacityLimited || DeliveryConflictOverflow is not null || DeliveryConflicts.Any(conflict => !conflict.IsResolved);

    /// <summary>The tenant identity of the session retained by a global projection scope.</summary>
    public string? OperationTenantId { get; init; }
}
