using Microsoft.CodeAnalysis;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiDiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MissingRouteTenant = new(
        "HESREST001",
        "Route tenant source requires a tenant route parameter",
        "REST API tenant source is Route, but '{0}' has no '{{tenant}}' or '{{tenantId}}' route parameter",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousQueryRoute = new(
        "HESREST002",
        "Query route parameters are ambiguous",
        "Query contract '{0}' has multiple non-tenant route parameters and no deterministic aggregate/entity mapping",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static Diagnostic CreateMissingRouteTenant(RestApiMessageDescriptor message)
        => Diagnostic.Create(MissingRouteTenant, Location.None, message.TypeName);

    public static Diagnostic CreateAmbiguousQueryRoute(RestApiMessageDescriptor message)
        => Diagnostic.Create(AmbiguousQueryRoute, Location.None, message.TypeName);

}
