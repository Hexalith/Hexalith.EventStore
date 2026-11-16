namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Activated catalog and profile-scoped AD-34 inventory/register identities.</summary>
/// <param name="Generation">The required generation.</param>
/// <param name="RootDigest">The required root digest.</param>
/// <param name="RouteDigest">The required route digest.</param>
/// <param name="IdempotencyDigest">The required idempotency digest.</param>
/// <param name="ContentDigest">The required content digest.</param>
/// <param name="Ad34InventoryDigest">The required ad34 inventory digest.</param>
/// <param name="Ad34RegisterDigest">The required ad34 register digest.</param>
/// <param name="EvidenceIds">The required evidence ids.</param>
public sealed record ProductionProfileCatalogs(
    int Generation,
    string RootDigest,
    string RouteDigest,
    string IdempotencyDigest,
    string ContentDigest,
    string Ad34InventoryDigest,
    string Ad34RegisterDigest,
    IReadOnlyList<string> EvidenceIds);
