namespace Hexalith.EventStore.Admin.Cli.Profiles;

/// <summary>
/// The on-disk profile store schema. Version field enables future migrations.
/// </summary>
public record ProfileStore(int Version, string? ActiveProfile, Dictionary<string, ConnectionProfile> Profiles) {
    /// <summary>Current schema version written by this CLI.</summary>
    public const int CurrentVersion = 1;
}
