using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Routing;

/// <summary>An explicit, value-free Development composition binding.</summary>
/// <param name="RouteId">Stable declaration identifier.</param>
/// <param name="AppId">Explicit target app.</param>
/// <param name="Method">Explicit method; query bindings must name their actual query method.</param>
/// <param name="ContractVersion">Explicit target contract version.</param>
/// <param name="IsRuntimeOverride">Whether this binding is a declared Development override.</param>
/// <param name="ResolverTenantPattern">Observed resolver scope in schema v2: exact historical tenant or wildcard. Never a synthesized request scope.</param>
public sealed record RouteCompositionBinding(string RouteId, string AppId, string Method, string ContractVersion, bool IsRuntimeOverride,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResolverTenantPattern = null);
