namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Resolves environment variables for the DAPR/Aspire integration-test harness, preferring the
/// platform-scoped <c>HEXALITH_EVENTSTORE_TEST_*</c> names while still honoring the legacy
/// <c>HEXALITH_TENANTS_TEST_*</c> names for one release so existing developer/CI setups keep working.
/// </summary>
internal static class DaprTestEnvironment {
    /// <summary>
    /// Reads the value of <paramref name="preferredName"/>, falling back to <paramref name="legacyName"/>
    /// when the preferred variable is unset or blank.
    /// </summary>
    /// <param name="preferredName">The current, platform-scoped variable name.</param>
    /// <param name="legacyName">The deprecated variable name kept for backward compatibility.</param>
    /// <returns>The resolved value, or <see langword="null"/> when neither variable is set.</returns>
    public static string? GetVariable(string preferredName, string legacyName) {
        string? value = Environment.GetEnvironmentVariable(preferredName);
        if (!string.IsNullOrWhiteSpace(value)) {
            return value;
        }

        return Environment.GetEnvironmentVariable(legacyName);
    }
}
