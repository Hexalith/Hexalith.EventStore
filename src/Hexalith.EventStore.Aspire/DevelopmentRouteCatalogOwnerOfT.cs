using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>The actual selected domain resource and its complete compiled contract and command-adapter owners.</summary>
/// <typeparam name="TProject">The .NET application resource type.</typeparam>
public sealed record DevelopmentRouteCatalogOwner<TProject>(IResourceBuilder<TProject> Project, Assembly Contracts, Assembly Adapters)
    where TProject : class, IDotnetProgramResource, IResourceWithEnvironment;
