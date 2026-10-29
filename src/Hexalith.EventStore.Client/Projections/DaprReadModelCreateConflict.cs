using Hexalith.EventStore.Contracts.Routing;

using Dapr.Client;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Resolves a failed create-only write without assuming a provider's initial ETag convention.</summary>
internal static class DaprReadModelCreateConflict {
    /// <summary>Returns true only when an authoritative read proves the addressed key already exists.</summary>
    internal static async Task<bool> ExistsAsync(DaprClient client, string storeName, string key, CancellationToken cancellationToken) {
        try {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            cancellationToken.ThrowIfCancellationRequested();
            (_, string etag) = await RouteCatalogRemoteExecution.InvokeAsync(() => client.GetByteStateAndETagAsync(storeName, key, consistencyMode: ConsistencyMode.Strong, cancellationToken: cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return !string.IsNullOrEmpty(etag);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested) {
            // A missing or unreadable key cannot turn an ambiguous write failure into a CAS rejection.
            return false;
        }
    }
}
