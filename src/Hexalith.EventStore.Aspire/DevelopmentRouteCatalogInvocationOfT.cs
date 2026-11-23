using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Hexalith.EventStore.Aspire;

/// <summary>An owner-declared required incoming call, bound to the actual selected caller resource.</summary>
/// <typeparam name="TProject">The .NET application resource type.</typeparam>
public sealed record DevelopmentRouteCatalogInvocation<TProject>(IResourceBuilder<TProject> Caller, string Method, IReadOnlyList<string> HttpMethods)
    where TProject : class, IDotnetProgramResource, IResourceWithEnvironment;
