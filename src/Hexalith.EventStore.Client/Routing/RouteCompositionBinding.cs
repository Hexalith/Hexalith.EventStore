namespace Hexalith.EventStore.Client.Routing;

/// <summary>An explicit, value-free Development composition binding.</summary>
/// <param name="RouteId">Stable declaration identifier.</param>
/// <param name="AppId">Explicit target app.</param>
/// <param name="Method">Explicit method; query bindings must name their actual query method.</param>
/// <param name="ContractVersion">Explicit target contract version.</param>
/// <param name="IsRuntimeOverride">Whether this binding is a declared Development override.</param>
public sealed record RouteCompositionBinding(string RouteId, string AppId, string Method, string ContractVersion, bool IsRuntimeOverride);
