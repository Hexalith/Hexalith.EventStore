using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>An owner-declared required incoming call, bound to the actual selected caller resource.</summary>
public sealed record DevelopmentRouteCatalogInvocation(IResourceBuilder<ProjectResource> Caller, string Method, IReadOnlyList<string> HttpMethods);
