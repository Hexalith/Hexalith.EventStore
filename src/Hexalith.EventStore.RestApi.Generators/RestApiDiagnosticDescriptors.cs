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

    public static readonly DiagnosticDescriptor DuplicateRoute = new(
        "HESREST003",
        "Generated REST route is duplicated",
        "REST contract '{0}' emits duplicate route '{1}'",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateParameter = new(
        "HESREST004",
        "Generated action identifiers are duplicated",
        "REST contract '{0}' has route, query, or method identifiers that generate duplicate action identifier '{1}'",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedQueryParameter = new(
        "HESREST005",
        "Query parameter type is not supported",
        "Query contract '{0}' has property '{1}' with type '{2}', which cannot be bound from query string",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static Diagnostic CreateMissingRouteTenant(RestApiMessageDescriptor message)
        => Diagnostic.Create(MissingRouteTenant, Location.None, message.TypeName);

    public static Diagnostic CreateAmbiguousQueryRoute(RestApiMessageDescriptor message)
        => Diagnostic.Create(AmbiguousQueryRoute, Location.None, message.TypeName);

    public static Diagnostic CreateDuplicateRoute(RestApiMessageDescriptor message, string route)
        => Diagnostic.Create(DuplicateRoute, Location.None, message.TypeName, route);

    public static Diagnostic CreateDuplicateParameter(RestApiMessageDescriptor message, string parameterName)
        => Diagnostic.Create(DuplicateParameter, Location.None, message.TypeName, parameterName);

    public static Diagnostic CreateUnsupportedQueryParameter(RestApiMessageDescriptor message, RestApiBindablePropertyDescriptor property)
        => Diagnostic.Create(UnsupportedQueryParameter, Location.None, message.TypeName, property.Name, property.TypeName);

}
