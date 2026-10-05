namespace Hexalith.EventStore.Client.Events;

/// <summary>One dependency selected by a deployment loader, including its observed outgoing graph edges.</summary>
/// <remarks>Only a trusted, locked loader may supply a complete graph for readiness.</remarks>
internal sealed record EventResolvedDependency(
    EventDependencyIdentity Identity,
    string ResolvedVersionOrAbiIdentity,
    string LoaderContextId,
    string ResolvedFile,
    IReadOnlyList<EventDependencyIdentity> Dependencies);
