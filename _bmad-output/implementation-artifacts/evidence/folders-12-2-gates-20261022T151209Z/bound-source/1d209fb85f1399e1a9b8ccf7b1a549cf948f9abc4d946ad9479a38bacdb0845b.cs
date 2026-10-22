namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Applies an explicit test-process HotReload override without changing shared Dapr configuration.</summary>
internal static class DaprTestRuntimeConfiguration
{
    /// <summary>Creates a private runtime configuration only when the caller supplies the exact optional override.</summary>
    internal static string? Create(string componentDirectory, string? configured)
    {
        configured = NormalizeHotReload(configured);
        if (configured is null)
        {
            return null;
        }

        string path = Path.Combine(componentDirectory, "test-runtime.yaml");
        File.WriteAllText(path, $"""
            apiVersion: dapr.io/v1alpha1
            kind: Configuration
            metadata:
              name: eventstore-test-runtime
            spec:
              features:
                - name: HotReload
                  enabled: {configured.ToLowerInvariant()}
            """ + "\n");
        return path;
    }

    /// <summary>Validates the optional process-local override shared by the two disposable live fixtures.</summary>
    internal static string? NormalizeHotReload(string? configured)
        => configured is null
            ? null
            : string.Equals(configured, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(configured, "false", StringComparison.OrdinalIgnoreCase)
                ? configured.ToLowerInvariant()
                : throw new InvalidOperationException("EVENTSTORE_TEST_DAPR_HOT_RELOAD must be true or false when supplied.");
}
