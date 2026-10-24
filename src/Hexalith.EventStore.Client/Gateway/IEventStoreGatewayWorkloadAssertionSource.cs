namespace Hexalith.EventStore.Client.Gateway;

/// <summary>Supplies a short-lived, tenant-bound assertion for one gateway operation.</summary>
public interface IEventStoreGatewayWorkloadAssertionSource
{
    /// <summary>Returns an assertion, or null when credentials are unavailable.</summary>
    ValueTask<string?> IssueAsync(string operation, string tenant, CancellationToken cancellationToken);

    /// <summary>Returns an assertion for the exact command or stream domain. Existing fixed-domain sources retain their behavior.</summary>
    ValueTask<string?> IssueAsync(string operation, string tenant, string domain, CancellationToken cancellationToken)
        => IssueAsync(operation, tenant, cancellationToken);
}
