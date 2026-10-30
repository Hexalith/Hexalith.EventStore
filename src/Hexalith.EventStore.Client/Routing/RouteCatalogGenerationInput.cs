using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>Explicit compiled owner inputs and selected deployment bindings for schema three.</summary>
/// <param name="Generation">Positive successor generation.</param>
/// <param name="Profile">Explicit runtime profile.</param>
/// <param name="Declarations">Complete selected owner declaration closure.</param>
/// <param name="Operations">Owner schemas and eligibility, with their original digests.</param>
/// <param name="Bindings">Exact selected targets.</param>
/// <param name="Idempotency">Executable command policies for this generation.</param>
/// <param name="CommittedPredecessor">Optional canonical previously committed bytes; always independently validated.</param>
/// <param name="CommittedPredecessorRootDigest">The independently retained committed root; required with predecessor bytes.</param>
public sealed record RouteCatalogGenerationInput(long Generation, string Profile,
    IReadOnlyList<RouteContractDeclaration> Declarations, IReadOnlyList<RouteOperationContract> Operations,
    IReadOnlyList<RouteCompositionBinding> Bindings, IReadOnlyList<IdempotencyCatalogEntry> Idempotency,
    byte[]? CommittedPredecessor = null, string? CommittedPredecessorRootDigest = null);
