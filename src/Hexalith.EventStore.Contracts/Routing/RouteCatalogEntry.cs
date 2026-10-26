namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>A deployment binding joined to exactly one declaration.</summary>
/// <param name="RouteId">Stable declaration join.</param>
/// <param name="Generation">Complete envelope generation.</param>
/// <param name="DeclarationDigest">Digest produced by the shared codec.</param>
/// <param name="AppId">Explicit target Dapr application identifier.</param>
/// <param name="Method">Explicit target invocation method.</param>
/// <param name="ContractVersion">Target version matching the declaration.</param>
/// <param name="CredentialKind">Target credential matching the declaration.</param>
/// <param name="Operation">Target operation matching the declaration.</param>
/// <param name="IsRuntimeOverride">Whether this binding is a Development-only explicit override.</param>
public sealed record RouteCatalogEntry(
    string RouteId,
    long Generation,
    string DeclarationDigest,
    string AppId,
    string Method,
    string ContractVersion,
    string CredentialKind,
    string Operation,
    bool IsRuntimeOverride);
