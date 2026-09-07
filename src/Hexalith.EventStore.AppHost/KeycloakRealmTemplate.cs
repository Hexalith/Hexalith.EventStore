using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Renders an inert checked-in Keycloak realm template into a per-run temporary import directory.
/// </summary>
internal sealed class KeycloakRealmTemplate : IDisposable
{
    private const string OwnerMarker = "hexalith-eventstore-keycloak-v1";
    private const string OwnerMarkerFileName = ".hexalith-owned";
    private const string RealmFileName = "hexalith-realm.json";
    private const string RunDirectoryPrefix = "run-";
    private static readonly UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly UnixFileMode SecureFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _directory;

    private KeycloakRealmTemplate(string directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Gets the generated import directory.
    /// </summary>
    public string ImportDirectory => _directory;

    /// <summary>
    /// Renders the realm template with the current run's identities and passwords.
    /// </summary>
    /// <param name="sourceDirectory">The checked-in realm-template directory.</param>
    /// <param name="credentials">The current run's generated credential set.</param>
    /// <returns>A disposable rendered realm directory.</returns>
    public static KeycloakRealmTemplate Render(
        string sourceDirectory,
        LocalAuthenticationCredentials credentials,
        Action<string>? beforeCommit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentNullException.ThrowIfNull(credentials);

        string sourcePath = Path.GetFullPath(sourceDirectory);
        string templatePath = Path.Combine(sourcePath, "hexalith-realm.json");
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("The Keycloak realm template was not found.", templatePath);
        }

        string rendered = File.ReadAllText(templatePath);
        IReadOnlyDictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__HEXALITH_ADMIN_USER_ID__"] = credentials.AdminUserId,
            ["__HEXALITH_ADMIN_USERNAME__"] = credentials.AdminUsername,
            ["__HEXALITH_ADMIN_PASSWORD__"] = credentials.AdminPassword,
            ["__HEXALITH_TENANT_A_USER_ID__"] = credentials.TenantAUserId,
            ["__HEXALITH_TENANT_A_PASSWORD__"] = credentials.TenantAPassword,
            ["__HEXALITH_TENANT_B_USER_ID__"] = credentials.TenantBUserId,
            ["__HEXALITH_TENANT_B_PASSWORD__"] = credentials.TenantBPassword,
            ["__HEXALITH_READ_ONLY_USER_ID__"] = credentials.ReadOnlyUserId,
            ["__HEXALITH_READ_ONLY_PASSWORD__"] = credentials.ReadOnlyPassword,
            ["__HEXALITH_NO_TENANT_USER_ID__"] = credentials.NoTenantUserId,
            ["__HEXALITH_NO_TENANT_PASSWORD__"] = credentials.NoTenantPassword,
        };

        foreach (KeyValuePair<string, string> replacement in replacements)
        {
            if (!rendered.Contains(replacement.Key, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The Keycloak realm template is missing a required inert placeholder.");
            }

            string escapedValue = JsonSerializer.Serialize(replacement.Value)[1..^1];
            rendered = rendered.Replace(replacement.Key, escapedValue, StringComparison.Ordinal);
        }

        if (rendered.Contains("__HEXALITH_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Keycloak realm template contains an unknown inert placeholder.");
        }

        string rootDirectory = Path.Combine(Path.GetTempPath(), "hexalith-eventstore-keycloak");
        CreateSecureDirectory(rootDirectory);
        CleanupStaleOwnedRunDirectories(rootDirectory, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1)));

        string directory = Path.Combine(
            rootDirectory,
            RunDirectoryPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant());
        try
        {
            CreateSecureDirectory(directory);
            WriteSecureFile(Path.Combine(directory, OwnerMarkerFileName), OwnerMarker);

            string temporaryPath = Path.Combine(
                directory,
                $".{RealmFileName}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}.tmp");
            WriteSecureFile(temporaryPath, rendered);
            beforeCommit?.Invoke(temporaryPath);
            File.Move(temporaryPath, Path.Combine(directory, RealmFileName));

            return new KeycloakRealmTemplate(directory);
        }
        catch
        {
            DeleteOwnedDirectory(directory, requireMarker: false);
            throw;
        }
    }

    /// <summary>
    /// Removes stale renderer-owned run directories without following or deleting unrelated paths.
    /// </summary>
    /// <param name="rootDirectory">The renderer's dedicated temporary root.</param>
    /// <param name="olderThan">Only owned directories older than this timestamp are removed.</param>
    internal static void CleanupStaleOwnedRunDirectories(string rootDirectory, DateTimeOffset olderThan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (string candidate in Directory.EnumerateDirectories(
            rootDirectory,
            $"{RunDirectoryPrefix}*",
            SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(candidate);
            if (!IsOwnedRunDirectoryName(name)
                || File.GetLastWriteTimeUtc(candidate) >= olderThan.UtcDateTime
                || (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            DeleteOwnedDirectory(candidate, requireMarker: true);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DeleteOwnedDirectory(_directory, requireMarker: true);
    }

    private static void CreateSecureDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(path);
            return;
        }

        _ = Directory.CreateDirectory(path, DirectoryMode);
        File.SetUnixFileMode(path, DirectoryMode);
    }

    private static void WriteSecureFile(string path, string content)
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.CreateNew,
            Share = FileShare.None,
            Options = FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = SecureFileMode;
        }

        using var stream = new FileStream(path, options);
        using (var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true))
        {
            writer.Write(content);
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);
    }

    private static void DeleteOwnedDirectory(string directory, bool requireMarker)
    {
        if (!Directory.Exists(directory)
            || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
        {
            return;
        }

        string markerPath = Path.Combine(directory, OwnerMarkerFileName);
        if (requireMarker
            && (!File.Exists(markerPath)
                || !string.Equals(File.ReadAllText(markerPath), OwnerMarker, StringComparison.Ordinal)))
        {
            return;
        }

        string[] subdirectories = Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
        if (subdirectories.Length != 0
            || files.Any(file => !IsOwnedFile(Path.GetFileName(file)))
            || files.Any(file => (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0))
        {
            return;
        }

        foreach (string file in files)
        {
            File.Delete(file);
        }

        Directory.Delete(directory, recursive: false);
    }

    private static bool IsOwnedFile(string fileName)
        => fileName is OwnerMarkerFileName or RealmFileName
            || (fileName.StartsWith($".{RealmFileName}.", StringComparison.Ordinal)
                && fileName.EndsWith(".tmp", StringComparison.Ordinal));

    private static bool IsOwnedRunDirectoryName(string name)
    {
        if (!name.StartsWith(RunDirectoryPrefix, StringComparison.Ordinal)
            || name.Length != RunDirectoryPrefix.Length + 24)
        {
            return false;
        }

        return name.AsSpan(RunDirectoryPrefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }
}
