using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>Complete explicit offline construction inputs; never populated from inferred domain policy.</summary>
/// <param name="Generation">Positive catalog generation.</param>
/// <param name="AllowRuntimeOverrides">Explicit Development override allowance.</param>
/// <param name="Declarations">Authoritative contract declarations.</param>
/// <param name="Bindings">Explicit composition bindings.</param>
/// <param name="Idempotency">Owner-declared complete AD-25 metadata.</param>
public sealed record DevelopmentRouteCatalogInput(
    long Generation,
    bool AllowRuntimeOverrides,
    IReadOnlyList<RouteContractDeclaration> Declarations,
    IReadOnlyList<RouteCompositionBinding> Bindings,
    IReadOnlyList<IdempotencyCatalogEntry> Idempotency);
