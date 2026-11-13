namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Isolates bounded shared-pool allocation and loan-ownership controls from unrelated image borrowers.</summary>
[CollectionDefinition("RouteCatalogImageBufferPool", DisableParallelization = true)]
public sealed class RouteCatalogImageBufferPoolCollection;
