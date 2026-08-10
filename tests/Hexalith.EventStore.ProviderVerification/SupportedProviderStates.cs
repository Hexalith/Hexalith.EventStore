namespace Hexalith.EventStore.ProviderVerification;

internal static class SupportedProviderStates
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "command-accepted",
        "command-validation-failure",
        "command-unauthorized",
        "command-forbidden",
        "command-not-found",
        "command-conflict",
        "command-rate-limited",
        "command-unexpected-5xx",
        "tenant-mismatch",
        "query-fresh-data",
        "query-empty-result",
        "query-malformed-payload",
        "query-forbidden",
        "query-not-found",
        "query-rate-limited",
        "query-etag-match",
        "query-etag-no-cache",
        "query-large-valid-metadata",
        "query-auth-tenant",
    };

    public static string RequireActive(ProviderStateCoordinator coordinator)
    {
        string? state = coordinator.CurrentState;
        return state is not null && All.Contains(state)
            ? state
            : throw new InvalidOperationException("provider-state-unavailable");
    }
}
