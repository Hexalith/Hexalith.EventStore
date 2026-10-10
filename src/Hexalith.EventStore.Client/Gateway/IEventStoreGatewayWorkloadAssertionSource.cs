namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Supplies a short-lived, tenant-bound assertion for one gateway operation.</summary>
public interface IEventStoreGatewayWorkloadAssertionSource
{
    /// <summary>Returns an assertion, or null when credentials are unavailable.</summary>
    ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken);
}
