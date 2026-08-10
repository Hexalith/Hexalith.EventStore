using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class StatefulETagService(ProviderStateCoordinator coordinator) : IETagService
{
    public Task<string?> GetCurrentETagAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? value = SupportedProviderStates.RequireActive(coordinator) switch
        {
            "query-etag-match" => "etag-cache-1",
            "query-etag-no-cache" => "etag-caller-1",
            "query-fresh-data" or "query-empty-result" or "query-large-valid-metadata" => "etag-query-1",
            "query-malformed-payload" or "query-forbidden" or "query-not-found" or "query-rate-limited"
                or "query-auth-tenant" => null,
            _ => throw new InvalidOperationException("provider-etag-state-unsupported"),
        };
        return Task.FromResult(value);
    }
}
