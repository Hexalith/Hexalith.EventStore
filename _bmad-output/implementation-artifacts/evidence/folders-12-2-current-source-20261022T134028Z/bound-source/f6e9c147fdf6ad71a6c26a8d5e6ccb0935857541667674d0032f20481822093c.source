namespace Hexalith.EventStore.Server.Actors;

/// <summary>Classifies an actor-owned metadata-only source inspection.</summary>
public enum SourceProofInspectionStatus
{
    /// <summary>The actor has validated supported persisted evidence.</summary>
    Available,

    /// <summary>Evidence is absent or its codec is unsupported.</summary>
    Unsupported,

    /// <summary>Persisted metadata and source evidence disagree.</summary>
    Invalid,
}
