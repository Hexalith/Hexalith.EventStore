namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>An independently captured generated public query endpoint, protected by the SDK action filter.</summary>
/// <param name="Pattern">The actual ASP.NET route template.</param>
/// <param name="RouteId">The compiled owner query identity.</param>
/// <param name="DeclarationDigest">The complete compiled owner declaration digest.</param>
/// <param name="OperationDigest">The compiled public schema and eligibility digest.</param>
/// <param name="ContractType">The actual generated action's request type.</param>
/// <param name="Policies">The actual authorization policies, including the real SDK human policy.</param>
public sealed record RouteCatalogGeneratedRestEndpoint(string Pattern, string RouteId, string DeclarationDigest,
    string OperationDigest, string ContractType, IReadOnlyList<string> Policies);
