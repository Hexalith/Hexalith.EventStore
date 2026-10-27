namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Schema v2 execution metadata for one actual handler; outputs are not independently dispatchable routes.</summary>
/// <param name="HandlerType">Qualified CLR identity of the owner handler or command type.</param>
/// <param name="AliasPolicy">Exactly exact or clr-short-name-v1, matching the existing aggregate dispatcher.</param>
/// <param name="Aliases">Finite accepted lookup keys for this actual handler.</param>
/// <param name="TargetKind">Exactly domain-service or projection-actor.</param>
/// <param name="QueryKind">Projection or computed for queries; null otherwise.</param>
/// <param name="ProjectionOutputs">Complete outputs of a projection dispatch; empty for other routes.</param>
/// <param name="ActorType">Exact actor type for actor targets; null for HTTP handlers.</param>
/// <param name="RequiresDelegatedContext">Whether an internal query requires the existing delegated caller context.</param>
public sealed record RouteExecutionDeclaration(
    string HandlerType,
    string AliasPolicy,
    IReadOnlyList<string> Aliases,
    string TargetKind,
    string? QueryKind,
    IReadOnlyList<string> ProjectionOutputs,
    string? ActorType = null,
    bool RequiresDelegatedContext = false);
