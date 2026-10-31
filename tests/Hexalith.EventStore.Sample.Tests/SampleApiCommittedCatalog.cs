using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.ServiceDefaults.Routing;

namespace Hexalith.EventStore.Sample.Tests;

/// <summary>Strict immutable tenant-a authority for API response/authentication fixtures; never production activation.</summary>
internal sealed class SampleApiCommittedCatalog : IRouteCatalogAuthority
{
    private readonly CommittedRouteCatalogSnapshot _snapshot;
    private int _acquisitions;

    internal SampleApiCommittedCatalog(int schema = 2)
    {
        if (schema is not (2 or 3)) throw new ArgumentOutOfRangeException(nameof(schema));
        var declarations = CounterCatalogDeclarations.Routes;
        var operations = schema == 3 ? CounterCatalogDeclarations.Operations : null;
        var routes = declarations.Select(route => new RouteCatalogEntry(route.RouteId, 1,
            RouteCatalogCodec.ComputeDeclarationDigest(route), route.Execution!.TargetKind == "projection-actor" ? "eventstore" : "sample",
            route.Execution.TargetKind == "projection-actor" ? "QueryAsync" : "process", "v1", route.CredentialKind,
            route.Operation, false, "tenant-a", operations?.Single(operation => operation.RouteId == route.RouteId).Digest)).ToArray();
        var envelope = RouteCatalogCodec.Seal(new(schema, 1, "Development", false, declarations, routes,
            CounterCatalogDeclarations.Commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, 1)).ToArray(),
            string.Empty, string.Empty, operations, schema == 3 ? [] : null));
        _snapshot = new(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1);
    }

    internal int Acquisitions => Volatile.Read(ref _acquisitions);

    public Task<RouteCatalogDispatchLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _acquisitions);
        return Task.FromResult(new RouteCatalogDispatchLease(_snapshot, static () => Task.CompletedTask));
    }

    public Task<RouteCatalogGenerationReadback> ReadbackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var envelope = _snapshot.Envelope;
        return Task.FromResult(new RouteCatalogGenerationReadback(1, 1, _snapshot.RootDigest, envelope.RouteFacetDigest,
            envelope.IdempotencyFacetDigest, envelope.Profile, [], true));
    }

    public Task PrepareAsync(RouteCatalogActivation candidate, CancellationToken cancellationToken = default) => Task.FromException(Immutable());
    public Task<RouteCatalogHostAttestation> ReadyAsync(string attemptId, CancellationToken cancellationToken = default) => Task.FromException<RouteCatalogHostAttestation>(Immutable());
    public Task CommitAsync(string attemptId, CancellationToken cancellationToken = default) => Task.FromException(Immutable());
    public Task RollbackAsync(RouteCatalogRollbackRequest request, CancellationToken cancellationToken = default) => Task.FromException(Immutable());
    public Task ReconcileAsync(RouteCatalogQuiescenceRequest request, CancellationToken cancellationToken = default) => Task.FromException(Immutable());

    private static RouteCatalogUnavailableException Immutable() => new("Sample API fixtures accept only their immutable Counter catalog.");
}
