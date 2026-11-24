namespace Hexalith.EventStore.DomainService;

/// <summary>Restricts the finite Development receiver to explicitly granted canonical POST routes.</summary>
internal sealed record DevelopmentDomainRouteCatalogInvocationScope(IReadOnlySet<string> PostRoutes);
