namespace Hexalith.EventStore.Testing.Security;

/// <summary>
/// Story 22.7b — fixed sentinel values used to prove protected payload, snapshot state, key alias,
/// provider-private metadata, state-store keys, connection strings, and provider exception text
/// never leak through logs, traces, ProblemDetails, exceptions, assertion output, or docs examples.
/// Tests inject these sentinels into protection-service fakes and dictionary entries, then call
/// <see cref="AssertNoLeak(IEnumerable{string?})"/> against every captured string from public
/// surfaces (log output, exception messages, ProblemDetails JSON, etc.).
/// </summary>
public static class ProtectedDataLeakSentinel {
    /// <summary>Sentinel used as the plaintext form of a protected event payload.</summary>
    public const string ProtectedPayloadPlaintext = "PROTECTED_PAYLOAD_PLAINTEXT_MARKER_22_7B";

    /// <summary>Sentinel used as the plaintext form of a protected snapshot state.</summary>
    public const string ProtectedSnapshotPlaintext = "PROTECTED_SNAPSHOT_PLAINTEXT_MARKER_22_7B";

    /// <summary>Sentinel used as a key alias / key identifier that callers must treat as sensitive.</summary>
    public const string ProtectedKeyAlias = "PROTECTED_KEY_ALIAS_MARKER_22_7B";

    /// <summary>Sentinel used as a provider-private metadata blob (e.g., raw cipher text).</summary>
    public const string ProtectedProviderPrivateBlob = "PROTECTED_PROVIDER_PRIVATE_BLOB_MARKER_22_7B";

    /// <summary>Sentinel used as a state-store key that must not leak through diagnostics.</summary>
    public const string ProtectedStateStoreKey = "PROTECTED_STATE_STORE_KEY_MARKER_22_7B";

    /// <summary>Sentinel used as a connection string fragment that must not leak.</summary>
    public const string ProtectedConnectionString = "PROTECTED_CONNECTION_STRING_MARKER_22_7B";

    /// <summary>Sentinel used as provider exception text that must not leak through public surfaces.</summary>
    public const string ProtectedProviderExceptionText = "PROTECTED_PROVIDER_EXCEPTION_MARKER_22_7B";

    /// <summary>Returns every sentinel value as an array.</summary>
    /// <returns>All defined sentinels.</returns>
    public static IReadOnlyList<string> All() => new[] {
        ProtectedPayloadPlaintext,
        ProtectedSnapshotPlaintext,
        ProtectedKeyAlias,
        ProtectedProviderPrivateBlob,
        ProtectedStateStoreKey,
        ProtectedConnectionString,
        ProtectedProviderExceptionText,
    };

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when any captured string contains any
    /// sentinel. Intended for use inside test assertions.
    /// </summary>
    /// <param name="captured">Captured strings (log lines, exception messages, ProblemDetails JSON, etc.).</param>
    public static void AssertNoLeak(IEnumerable<string?> captured) {
        ArgumentNullException.ThrowIfNull(captured);
        IReadOnlyList<string> sentinels = All();
        foreach (string? entry in captured) {
            if (string.IsNullOrEmpty(entry)) {
                continue;
            }

            for (int sentinelIndex = 0; sentinelIndex < sentinels.Count; sentinelIndex++) {
                string sentinel = sentinels[sentinelIndex];
                if (entry.Contains(sentinel, StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        $"Protected-data sentinel at index {sentinelIndex} was found in captured output. This indicates a no-leak guarantee was violated.");
                }
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when none of the supplied strings contains any sentinel.
    /// </summary>
    /// <param name="captured">Captured strings.</param>
    /// <returns><see langword="true"/> when no leak is detected.</returns>
    public static bool HasNoLeak(IEnumerable<string?> captured) {
        ArgumentNullException.ThrowIfNull(captured);
        IReadOnlyList<string> sentinels = All();
        return !captured.Any(entry => !string.IsNullOrEmpty(entry) && sentinels.Any(s => entry!.Contains(s, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Story 22.7d-4 — asserts no sentinel appears anywhere in the file at <paramref name="filePath"/>.
    /// On leak the thrown exception reports the sentinel index and file path, but never the sentinel value.
    /// </summary>
    /// <param name="filePath">Path to a UTF-8 text file (evidence artifact, doc, captured stdout/stderr, etc.).</param>
    public static void AssertNoLeakInFile(string filePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) {
            throw new FileNotFoundException(
                $"Protected-data sentinel scan target does not exist: {filePath}",
                filePath);
        }

        string content = File.ReadAllText(filePath);
        IReadOnlyList<string> sentinels = All();
        for (int sentinelIndex = 0; sentinelIndex < sentinels.Count; sentinelIndex++) {
            if (content.Contains(sentinels[sentinelIndex], StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"Protected-data sentinel at index {sentinelIndex} was found in file '{filePath}'. This indicates a no-leak guarantee was violated. The sentinel value is not echoed here to keep failure output safe.");
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the file at <paramref name="filePath"/> contains no sentinel value.
    /// Returns <see langword="false"/> when the file does not exist.
    /// </summary>
    /// <param name="filePath">Path to scan.</param>
    /// <returns><see langword="true"/> when no leak is detected.</returns>
    public static bool HasNoLeakInFile(string filePath) {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) {
            return false;
        }

        string content = File.ReadAllText(filePath);
        IReadOnlyList<string> sentinels = All();
        for (int sentinelIndex = 0; sentinelIndex < sentinels.Count; sentinelIndex++) {
            if (content.Contains(sentinels[sentinelIndex], StringComparison.Ordinal)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Story 22.7d-4 — asserts no sentinel appears in any file matched by the supplied glob patterns
    /// rooted under <paramref name="directory"/>. Throws <see cref="InvalidOperationException"/> on the
    /// first leak and reports the offending file plus sentinel index, never the sentinel value itself.
    /// </summary>
    /// <param name="directory">Root directory to scan.</param>
    /// <param name="searchPatterns">
    /// Optional search patterns relative to <paramref name="directory"/> (e.g. <c>*.md</c>, <c>*.json</c>).
    /// When empty, every file under the directory is scanned recursively.
    /// </param>
    public static void AssertNoLeakInDirectory(string directory, params string[] searchPatterns) {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) {
            throw new DirectoryNotFoundException(
                $"Protected-data sentinel scan target directory does not exist: {directory}");
        }

        IReadOnlyList<string> sentinels = All();
        IEnumerable<string> files = (searchPatterns is null || searchPatterns.Length == 0)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            : searchPatterns.SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (string file in files) {
            string content;
            try {
                content = File.ReadAllText(file);
            }
            catch (IOException ex) {
                throw new InvalidOperationException(
                    $"Protected-data sentinel scan could not read file '{file}' (scanned from directory '{directory}'). FailureType={ex.GetType().Name}; failing closed.",
                    ex);
            }
            catch (UnauthorizedAccessException ex) {
                throw new InvalidOperationException(
                    $"Protected-data sentinel scan could not read file '{file}' (scanned from directory '{directory}'). FailureType={ex.GetType().Name}; failing closed.",
                    ex);
            }

            for (int sentinelIndex = 0; sentinelIndex < sentinels.Count; sentinelIndex++) {
                if (content.Contains(sentinels[sentinelIndex], StringComparison.Ordinal)) {
                    throw new InvalidOperationException(
                        $"Protected-data sentinel at index {sentinelIndex} was found in file '{file}' (scanned from directory '{directory}'). The sentinel value is not echoed here to keep failure output safe.");
                }
            }
        }
    }
}
