namespace Hexalith.EventStore.Admin.Cli.Profiles;

/// <summary>
/// Thrown when the profile store version is newer than what this CLI supports.
/// </summary>
public class ProfileStoreVersionException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileStoreVersionException"/> class.
    /// </summary>
    public ProfileStoreVersionException(int version)
        : base($"Profile store version {version} is newer than this CLI supports. Please upgrade eventstore-admin.") => Version = version;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileStoreVersionException"/> class.
    /// </summary>
    public ProfileStoreVersionException()
        : base() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileStoreVersionException"/> class.
    /// </summary>
    public ProfileStoreVersionException(string message)
        : base(message) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileStoreVersionException"/> class.
    /// </summary>
    public ProfileStoreVersionException(string message, Exception innerException)
        : base(message, innerException) {
    }

    /// <summary>Gets the unsupported version number.</summary>
    public int Version { get; }
}
