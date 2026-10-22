using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class StatefulETagService(ProviderStateCoordinator coordinator) : IETagService
{
    internal const string CallerETag = "b3JkZXJz.Y2FsbGVyLTE";
    internal const string CacheETag = "b3JkZXJz.Y2FjaGUtMQ";
    internal const string QueryETag = "b3JkZXJz.cXVlcnktMQ";

    public Task<string?> GetCurrentETagAsync(
        string projectionType,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? value = SupportedProviderStates.RequireActive(coordinator) switch
        {
            "query-etag-match" => CacheETag,
            "query-etag-no-cache" => CallerETag,
            "query-fresh-data" or "query-empty-result" or "query-large-valid-metadata" => QueryETag,
            "query-malformed-payload" or "query-forbidden" or "query-not-found" or "query-rate-limited"
                or "query-auth-tenant" => null,
            _ => throw new InvalidOperationException("provider-etag-state-unsupported"),
        };
        return Task.FromResult(value);
    }
}
