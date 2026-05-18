using System;
using System.Collections.Generic;
using System.Linq;

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
}
