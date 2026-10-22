namespace Hexalith.EventStore.Server.Actors;

/// <summary>Persisted operation-owned candidate or promotion evidence returned without repair.</summary>
public enum ProjectionRebuildInspectionStatus
{
    /// <summary>No candidate or receipt exists.</summary>
    Missing,
    /// <summary>The matching immutable candidate is staged and not selected.</summary>
    Staged,
    /// <summary>The matching promotion receipt and live state agree.</summary>
    Promoted,
    /// <summary>A foreign operation or mismatched selected state prevents adoption.</summary>
    Conflict,
}
