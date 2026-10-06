namespace Hexalith.EventStore.Client.Events;

/// <summary>One caller-supplied resolved dependency and its declared outgoing graph edges.</summary>
/// <remarks>
/// Identity, version, context and edge claims must come from the reviewed deployment inventory.
/// A matching local graph does not establish completeness or execution from immutable artifacts.
/// </remarks>
/// <param name="Identity">The declared logical load identity and managed/native kind.</param>
/// <param name="ResolvedVersionOrAbiIdentity">The deployment's resolved version or ABI claim.</param>
/// <param name="LoaderContextId">The deployment's declared loader context.</param>
/// <param name="ResolvedFile">The caller-selected artifact path checked by the local verifier.</param>
/// <param name="Dependencies">The supplied outgoing dependency identities.</param>
internal sealed record EventResolvedDependency(
    EventDependencyIdentity Identity,
    string ResolvedVersionOrAbiIdentity,
    string LoaderContextId,
    string ResolvedFile,
    IReadOnlyList<EventDependencyIdentity> Dependencies);
