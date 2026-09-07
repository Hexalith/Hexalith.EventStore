using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.AppHost;

/// <summary>
/// Renders an inert checked-in Keycloak realm template into a per-run temporary import directory.
/// </summary>
internal sealed class KeycloakRealmTemplate : IDisposable
{
    private const string OwnerMarker = "hexalith-eventstore-keycloak-v1";
    private const string OwnerMarkerFileName = ".hexalith-owned";
    private const string OwnerLeaseFileName = ".hexalith-owner-lease";
    private const string RealmFileName = "hexalith-realm.json";
    private const string RunDirectoryPrefix = "run-";
    private static readonly UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly ConcurrentDictionary<string, byte> LiveOwnedDirectories = new(StringComparer.Ordinal);
    private static readonly Regex PlaceholderPattern = new(
        "__HEXALITH_[A-Z0-9_]+__",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly UnixFileMode SecureFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly Action<string>? _beforeDelete;
    private readonly string _directory;
    private FileStream? _ownerLease;
    private bool _disposed;
    private bool _ownerMarkerDeleted;

    private KeycloakRealmTemplate(
        string directory,
        FileStream ownerLease,
        Action<string>? beforeDelete)
    {
        _directory = directory;
        _ownerLease = ownerLease;
        _beforeDelete = beforeDelete;
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
    /// <param name="beforeCommit">An optional test callback invoked after the temporary file is secured.</param>
    /// <param name="temporaryRoot">An optional isolated temporary root used by focused tests.</param>
    /// <param name="beforeDelete">An optional test callback invoked immediately before an owned path is deleted.</param>
    /// <returns>A disposable rendered realm directory.</returns>
    public static KeycloakRealmTemplate Render(
        string sourceDirectory,
        LocalAuthenticationCredentials credentials,
        Action<string>? beforeCommit = null,
        string? temporaryRoot = null,
        Action<string>? beforeDelete = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentNullException.ThrowIfNull(credentials);
        if (temporaryRoot is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(temporaryRoot);
        }

        string sourcePath = Path.GetFullPath(sourceDirectory);
        string templatePath = Path.Combine(sourcePath, "hexalith-realm.json");
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException("The Keycloak realm template was not found.", templatePath);
        }

        string template = File.ReadAllText(templatePath);
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
        var replacedPlaceholders = new HashSet<string>(StringComparer.Ordinal);

        string rendered = PlaceholderPattern.Replace(template, match =>
        {
            if (!replacements.TryGetValue(match.Value, out string? value))
            {
                throw new InvalidOperationException(
                    "The Keycloak realm template contains an unknown inert placeholder.");
            }

            _ = replacedPlaceholders.Add(match.Value);
            return JsonSerializer.Serialize(value)[1..^1];
        });

        if (replacements.Keys.Any(placeholder => !replacedPlaceholders.Contains(placeholder)))
        {
            throw new InvalidOperationException(
                "The Keycloak realm template is missing a required inert placeholder.");
        }

        if (rendered.Contains("__HEXALITH_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Keycloak realm template contains an unknown inert placeholder.");
        }

        string rootDirectory = Path.GetFullPath(
            temporaryRoot ?? Path.Combine(Path.GetTempPath(), "hexalith-eventstore-keycloak"));
        CreateSecureDirectory(rootDirectory);
        CleanupStaleOwnedRunDirectories(rootDirectory, DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1)));

        string directory = Path.Combine(
            rootDirectory,
            RunDirectoryPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant());
        var ownedFiles = new HashSet<string>(StringComparer.Ordinal);
        FileStream? ownerLease = null;
        try
        {
            CreateSecureDirectory(directory);
            string markerPath = Path.Combine(directory, OwnerMarkerFileName);
            _ = ownedFiles.Add(markerPath);
            WriteSecureFile(markerPath, OwnerMarker);

            string leasePath = Path.Combine(directory, OwnerLeaseFileName);
            _ = ownedFiles.Add(leasePath);
            ownerLease = CreateOwnerLease(leasePath);

            string temporaryPath = Path.Combine(
                directory,
                $".{RealmFileName}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}.tmp");
            _ = ownedFiles.Add(temporaryPath);
            WriteSecureFile(temporaryPath, rendered);
            beforeCommit?.Invoke(temporaryPath);
            string realmPath = Path.Combine(directory, RealmFileName);
            File.Move(temporaryPath, realmPath);
            _ = ownedFiles.Remove(temporaryPath);
            _ = ownedFiles.Add(realmPath);
            if (!LiveOwnedDirectories.TryAdd(directory, 0))
            {
                throw new InvalidOperationException(
                    "The generated Keycloak realm directory is already owned by this process.");
            }

            return new KeycloakRealmTemplate(directory, ownerLease, beforeDelete);
        }
        catch
        {
            ownerLease?.Dispose();
            bool markerDeleted = false;
            DeleteOwnedDirectory(
                directory,
                ownedFiles,
                requireMarker: false,
                beforeDelete: null,
                ref markerDeleted);
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

        if (IsReparsePoint(rootDirectory))
        {
            throw new InvalidOperationException(
                "The Keycloak realm temporary root must not be a reparse point.");
        }

        foreach (string candidate in Directory.EnumerateDirectories(
            rootDirectory,
            $"{RunDirectoryPrefix}*",
            SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(candidate);
            if (!IsOwnedRunDirectoryName(name)
                || File.GetLastWriteTimeUtc(candidate) >= olderThan.UtcDateTime
                || LiveOwnedDirectories.ContainsKey(candidate)
                || (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string leasePath = Path.Combine(candidate, OwnerLeaseFileName);
            if (!TryAcquireCleanupLease(leasePath, out FileStream? cleanupLease))
            {
                continue;
            }

            if (OperatingSystem.IsMacOS())
            {
                cleanupLease?.Dispose();
                cleanupLease = null;
            }

            using (cleanupLease)
            {
                bool markerDeleted = false;
                DeleteOwnedDirectory(
                    candidate,
                    GetCompletedRenderOwnedFiles(candidate),
                    requireMarker: true,
                    beforeDelete: null,
                    ref markerDeleted);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ownerLease?.Dispose();
        _ownerLease = null;
        try
        {
            DeleteOwnedDirectory(
                _directory,
                GetCompletedRenderOwnedFiles(_directory),
                requireMarker: true,
                _beforeDelete,
                ref _ownerMarkerDeleted);
            _disposed = true;
            _ = LiveOwnedDirectories.TryRemove(_directory, out _);
        }
        catch (IOException)
        {
            _ownerLease = TryRestoreOwnerLease(_directory);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            _ownerLease = TryRestoreOwnerLease(_directory);
            throw;
        }
    }

    private static void CreateSecureDirectory(string path)
    {
        RejectReparsePoint(path);

        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(path);
            RejectReparsePoint(path);
            return;
        }

        _ = Directory.CreateDirectory(path, DirectoryMode);
        RejectReparsePoint(path);
        File.SetUnixFileMode(path, DirectoryMode);
    }

    private static FileStream CreateOwnerLease(string path)
        => OpenOwnerLease(path, FileMode.CreateNew);

    private static FileStream OpenOwnerLease(string path, FileMode mode)
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.ReadWrite,
            Mode = mode,
            Share = OperatingSystem.IsMacOS()
                ? FileShare.None
                : FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.WriteThrough,
        };
        if (!OperatingSystem.IsWindows() && mode == FileMode.CreateNew)
        {
            options.UnixCreateMode = SecureFileMode;
        }

        FileStream stream = new(path, options);
        try
        {
            if (mode == FileMode.CreateNew)
            {
                stream.WriteByte(1);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsMacOS())
            {
                stream.Lock(0, 1);
            }

            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
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

    private static void DeleteOwnedDirectory(
        string directory,
        IReadOnlyCollection<string> ownedFiles,
        bool requireMarker,
        Action<string>? beforeDelete,
        ref bool markerDeleted)
    {
        if (!Directory.Exists(directory)
            || IsReparsePoint(directory))
        {
            return;
        }

        string markerPath = Path.Combine(directory, OwnerMarkerFileName);
        if (requireMarker
            && !markerDeleted
            && (!File.Exists(markerPath)
                || !string.Equals(File.ReadAllText(markerPath), OwnerMarker, StringComparison.Ordinal)))
        {
            return;
        }

        foreach (string file in ownedFiles.Where(file => !string.Equals(file, markerPath, StringComparison.Ordinal)))
        {
            if (!File.Exists(file))
            {
                continue;
            }

            if (IsReparsePoint(file))
            {
                return;
            }

            beforeDelete?.Invoke(file);
            File.Delete(file);
        }

        if (!markerDeleted && File.Exists(markerPath))
        {
            if (IsReparsePoint(markerPath))
            {
                return;
            }

            beforeDelete?.Invoke(markerPath);
            File.Delete(markerPath);
            markerDeleted = true;
        }

        if (Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return;
        }

        beforeDelete?.Invoke(directory);
        Directory.Delete(directory, recursive: false);
    }

    private static IReadOnlyCollection<string> GetCompletedRenderOwnedFiles(string directory)
        =>
        [
            Path.Combine(directory, RealmFileName),
            Path.Combine(directory, OwnerLeaseFileName),
            Path.Combine(directory, OwnerMarkerFileName),
        ];

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool IsOwnedRunDirectoryName(string name)
    {
        if (!name.StartsWith(RunDirectoryPrefix, StringComparison.Ordinal)
            || name.Length != RunDirectoryPrefix.Length + 24)
        {
            return false;
        }

        return name.AsSpan(RunDirectoryPrefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    private static void RejectReparsePoint(string path)
    {
        if (IsReparsePoint(path))
        {
            throw new InvalidOperationException(
                "The Keycloak realm temporary path must not be a reparse point.");
        }
    }

    private static bool TryAcquireCleanupLease(string leasePath, out FileStream? cleanupLease)
    {
        cleanupLease = null;
        if (!File.Exists(leasePath))
        {
            return true;
        }

        if (IsReparsePoint(leasePath))
        {
            return false;
        }

        try
        {
            cleanupLease = new FileStream(
                leasePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                OperatingSystem.IsMacOS()
                    ? FileShare.None
                    : FileShare.ReadWrite | FileShare.Delete);
            if (!OperatingSystem.IsMacOS())
            {
                cleanupLease.Lock(0, 1);
            }

            return true;
        }
        catch (IOException)
        {
            cleanupLease?.Dispose();
            cleanupLease = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            cleanupLease?.Dispose();
            cleanupLease = null;
            return false;
        }
    }

    private static FileStream? TryRestoreOwnerLease(string directory)
    {
        string leasePath = Path.Combine(directory, OwnerLeaseFileName);
        if (!File.Exists(leasePath) || IsReparsePoint(leasePath))
        {
            return null;
        }

        try
        {
            return OpenOwnerLease(leasePath, FileMode.Open);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
