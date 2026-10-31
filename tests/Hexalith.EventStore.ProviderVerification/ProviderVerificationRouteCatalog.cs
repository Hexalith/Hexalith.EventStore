using System.Text.Json;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.ProviderVerification;

/// <summary>Supplies immutable, validated query authority for the two declared provider fixture tenants.</summary>
internal sealed class ProviderVerificationRouteCatalog(ProviderStateCoordinator coordinator) : IRouteCatalogAuthority
{
    private int _acquisitionCount;
    private readonly CommittedRouteCatalogSnapshot _ordinaryTenant = CreateSnapshot("tenant-contract-a");
    private readonly CommittedRouteCatalogSnapshot _propagatedTenant = CreateSnapshot("tenant-contract-case");

    /// <summary>Gets successful fixture acquisitions so HTTP tests can distinguish catalog and authorization denials.</summary>
    internal int AcquisitionCount => Volatile.Read(ref _acquisitionCount);

    /// <inheritdoc/>
    public Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommittedRouteCatalogSnapshot snapshot = SelectSnapshot();
        _ = Interlocked.Increment(ref _acquisitionCount);
        return Task.FromResult(new RouteCatalogDispatchLease(snapshot, static () => Task.CompletedTask));
    }

    /// <inheritdoc/>
    public Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommittedRouteCatalogSnapshot snapshot = SelectSnapshot();
        RouteCatalogEnvelope envelope = snapshot.Envelope;
        return Task.FromResult(new RouteCatalogGenerationReadback(snapshot.Epoch, envelope.Generation,
            snapshot.RootDigest, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest,
            envelope.Profile, [], true));
    }

    /// <inheritdoc/>
    public Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default)
        => Task.FromException(ImmutableFixture());

    /// <inheritdoc/>
    public Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default)
        => Task.FromException<RouteCatalogHostAttestation>(ImmutableFixture());

    /// <inheritdoc/>
    public Task CommitAsync(string attemptId, CancellationToken cancellationToken = default)
        => Task.FromException(ImmutableFixture());

    /// <inheritdoc/>
    public Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default)
        => Task.FromException(ImmutableFixture());

    /// <inheritdoc/>
    public Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default)
        => Task.FromException(ImmutableFixture());

    private CommittedRouteCatalogSnapshot SelectSnapshot()
        => SupportedProviderStates.RequireActive(coordinator) switch
        {
            "query-auth-tenant" => _propagatedTenant,
            "query-fresh-data" or "query-empty-result" or "query-malformed-payload"
                or "query-forbidden" or "query-not-found" or "query-rate-limited"
                or "query-etag-match" or "query-etag-no-cache" or "query-large-valid-metadata" => _ordinaryTenant,
            _ => throw ImmutableFixture(),
        };

    private static CommittedRouteCatalogSnapshot CreateSnapshot(string tenant)
    {
        var declaration = new RouteContractDeclaration("provider-orders-query", "query", "orders", "GetOrders",
            "orders", "v1", null, "workload", EventStoreOperationIdentifiers.DomainServiceQuery, null, [],
            new RouteExecutionDeclaration(typeof(StatefulQueryRouter).FullName!, "exact", ["GetOrders"],
                "domain-service", "projection", []));
        RouteOperationContract operation = RouteOperationSchema.Create(declaration,
            typeof(SubmitQueryRequest), typeof(SubmitQueryResponse), "public", "UI-only", JsonSerializerOptions.Web);
        var binding = new RouteCatalogEntry(declaration.RouteId, 1,
            RouteCatalogCodec.ComputeDeclarationDigest(declaration), "provider-verification", "query", "v1",
            declaration.CredentialKind, declaration.Operation, false, tenant, operation.Digest);
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(new RouteCatalogEnvelope(3, 1, "Development", false,
            [declaration], [binding], [], string.Empty, string.Empty, [operation], []));
        return new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope),
            RouteCatalogCodec.ComputeRootDigest(envelope), 1);
    }

    private static RouteCatalogUnavailableException ImmutableFixture()
        => new("Provider verification accepts only its immutable declared query fixture.");
}
