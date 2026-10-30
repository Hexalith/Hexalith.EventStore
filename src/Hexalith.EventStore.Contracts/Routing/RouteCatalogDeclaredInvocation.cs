namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>An existing owner-declared transport grant, captured independently of a proposed catalog or loaded ACL.</summary>
/// <param name="CallerAppId">The declared caller.</param>
/// <param name="Method">The effective declared Dapr path or prefix.</param>
/// <param name="HttpMethods">The declared, bounded HTTP verbs.</param>
public sealed record RouteCatalogDeclaredInvocation(string CallerAppId, string Method, IReadOnlyList<string> HttpMethods);
