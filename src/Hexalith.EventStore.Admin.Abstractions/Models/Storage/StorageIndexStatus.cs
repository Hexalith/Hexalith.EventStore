namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Indicates the quality of the operational storage index that produced a storage overview.
/// </summary>
public enum StorageIndexStatus {
    /// <summary>The index writer or source is unavailable.</summary>
    Unavailable = 0,

    /// <summary>The expected index writer has not populated the index yet.</summary>
    MissingWriter = 1,

    /// <summary>The index is available and intentionally empty.</summary>
    Empty = 2,

    /// <summary>The index is available and contains persisted activity.</summary>
    Populated = 3,
}
