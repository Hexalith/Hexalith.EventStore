using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>The actual selected domain resource and its complete compiled contract and command-adapter owners.</summary>
public sealed record DevelopmentRouteCatalogOwner(IResourceBuilder<ProjectResource> Project, Assembly Contracts, Assembly Adapters);
