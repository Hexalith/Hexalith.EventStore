using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Profiles;

/// <summary>
/// Static helper for reading and writing the <c>profiles.json</c> configuration file.
/// All methods are synchronous — the file is &lt; 1KB (same pattern as kubectl, gh, az).
/// </summary>
public static partial class ProfileManager {
    private static readonly Regex _profileNameRegex = ProfileNameRegex();
#pragma warning disable IDE0090 // Use 'new(...)'

    private static readonly HashSet<string> _reservedNames = new HashSet<string>(
#pragma warning restore IDE0090 // Use 'new(...)'
   [
        "version",
        "activeProfile",
        "profiles",
        "url",
        "token",
        "format",
    ]);

    /// <summary>
    /// Gets the default path to <c>~/.eventstore/profiles.json</c>.
    /// </summary>
    public static string GetDefaultProfilePath() {
        string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) {
            home = Environment.GetEnvironmentVariable("HOME");
        }

        if (string.IsNullOrEmpty(home)) {
            home = Environment.GetEnvironmentVariable("USERPROFILE");
        }

        if (string.IsNullOrEmpty(home)) {
            throw new InvalidOperationException("Cannot determine home directory. Set the HOME environment variable.");
        }

        return Path.Combine(home, ".eventstore", "profiles.json");
    }

    /// <summary>
    /// Loads the profile store from disk. Returns an empty store if the file is missing or corrupt.
    /// </summary>
    public static ProfileStore Load(string? profilePath = null) {
        string path = profilePath ?? GetDefaultProfilePath();
        if (!File.Exists(path)) {
            return new ProfileStore(ProfileStore.CurrentVersion, null, []);
        }

        try {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) {
                return new ProfileStore(ProfileStore.CurrentVersion, null, []);
            }

            ProfileStore? store = JsonSerializer.Deserialize<ProfileStore>(json, JsonDefaults.Options);
            if (store is null) {
                return new ProfileStore(ProfileStore.CurrentVersion, null, []);
            }

            if (store.Version > ProfileStore.CurrentVersion) {
                throw new ProfileStoreVersionException(store.Version);
            }

            return store with { Profiles = store.Profiles ?? [] };
        }
        catch (JsonException) {
            Console.Error.WriteLine($"Warning: Profile store at '{path}' contains invalid JSON. Treating as empty.");
            return new ProfileStore(ProfileStore.CurrentVersion, null, []);
        }
    }

    /// <summary>
    /// Masks a token for display: first 4 characters + "..." or "(none)" for null/empty.
    /// </summary>
    public static string MaskToken(string? token) {
        if (string.IsNullOrEmpty(token)) {
            return "(none)";
        }

        return token.Length <= 4 ? token + "..." : token[..4] + "...";
    }

    /// <summary>
    /// Saves the profile store to disk, creating the directory if needed.
    /// Sets Unix file permissions (700 dir, 600 file) on non-Windows platforms.
    /// </summary>
    public static void Save(ProfileStore store, string? profilePath = null) {
        ArgumentNullException.ThrowIfNull(store);
        string path = profilePath ?? GetDefaultProfilePath();
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
            DirectoryInfo dirInfo = Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) {
                dirInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            }
        }

        ProfileStore storeToWrite = store with { Version = ProfileStore.CurrentVersion };
        string json = JsonSerializer.Serialize(storeToWrite, JsonDefaults.Options);
        File.WriteAllText(path, json);

        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <summary>
    /// Validates a profile name against the pattern <c>^[a-zA-Z0-9_-]{1,64}$</c>.
    /// Rejects reserved names that would conflict with JSON schema keys in shell completions.
    /// </summary>
    public static bool ValidateProfileName(string name)
        => !string.IsNullOrEmpty(name) && _profileNameRegex.IsMatch(name) && !_reservedNames.Contains(name);

    [GeneratedRegex(@"^[a-zA-Z0-9_-]{1,64}$")]
    private static partial Regex ProfileNameRegex();
}
