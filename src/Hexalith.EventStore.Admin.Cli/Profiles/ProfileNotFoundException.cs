namespace Hexalith.EventStore.Admin.Cli.Profiles;

/// <summary>
/// Thrown when a named profile is not found in the profile store.
/// </summary>
public class ProfileNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileNotFoundException"/> class.
    /// </summary>
    public ProfileNotFoundException(string profileName)
        : base($"Profile '{profileName}' not found.")
    {
        ProfileName = profileName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileNotFoundException"/> class.
    /// </summary>
    public ProfileNotFoundException(string profileName, string message)
        : base(message)
    {
        ProfileName = profileName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileNotFoundException"/> class.
    /// </summary>
    public ProfileNotFoundException()
        : base()
    {
        ProfileName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileNotFoundException"/> class.
    /// </summary>
    public ProfileNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
        ProfileName = string.Empty;
    }

    /// <summary>Gets the profile name that was not found.</summary>
    public string ProfileName { get; }
}
