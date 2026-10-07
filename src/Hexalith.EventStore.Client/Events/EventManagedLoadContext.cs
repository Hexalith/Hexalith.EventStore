using System.Runtime.Loader;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Maps one reviewed loader-context identity to the runtime context observed by a host.</summary>
/// <remarks>The mapping is a local supplied claim and grants no manifest or deployment authority.</remarks>
/// <param name="LoaderContextId">The exact context identity in the dependency rows.</param>
/// <param name="Context">The actual managed loader context whose loads are observed.</param>
internal sealed record EventManagedLoadContext(string LoaderContextId, AssemblyLoadContext Context);
