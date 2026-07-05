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

    public static readonly DiagnosticDescriptor UnsupportedContractShape = new(
        "HESREST006",
        "REST contract shape is not supported",
        "REST contract '{0}' cannot be emitted because {1}",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidRouteTemplate = new(
        "HESREST007",
        "REST route template is invalid",
        "REST contract '{0}' has invalid route template '{1}': {2}",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedRestVerb = new(
        "HESREST008",
        "REST route verb is not supported",
        "REST contract '{0}' has unsupported route verb '{1}'",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnmappedRouteParameter = new(
        "HESREST009",
        "Command route parameter cannot be mapped",
        "Command contract '{0}' has route parameter '{1}' that cannot be mapped to AggregateId or a public command property",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateJsonName = new(
        "HESREST010",
        "REST payload JSON name is duplicated",
        "REST contract '{0}' has multiple properties that resolve to JSON name '{1}'",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnmappedQueryBindingRouteParameter = new(
        "HESREST011",
        "Query binding route parameter cannot be mapped",
        "Query contract '{0}' has query binding route parameter '{1}' that is not present in the generated REST route",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidQueryBinding = new(
        "HESREST012",
        "Query binding metadata is invalid",
        "Query contract '{0}' has invalid RestQueryBinding metadata: {1}",
        "Hexalith.EventStore.RestApi",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousRoutePropertyMatch = new(
        "HESREST013",
        "REST route parameter matches multiple properties",
        "REST contract '{0}' has route parameter '{1}' that matches multiple public properties by CLR or JSON name",
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

    public static Diagnostic CreateUnsupportedContractShape(RestApiMessageDescriptor message)
        => Diagnostic.Create(UnsupportedContractShape, Location.None, message.TypeName, message.UnsupportedReason ?? "its shape is unsupported");

    public static Diagnostic CreateInvalidRouteTemplate(RestApiMessageDescriptor message, string template, string reason)
        => Diagnostic.Create(InvalidRouteTemplate, Location.None, message.TypeName, template, reason);

    public static Diagnostic CreateUnsupportedRestVerb(RestApiMessageDescriptor message, string verb)
        => Diagnostic.Create(UnsupportedRestVerb, Location.None, message.TypeName, verb);

    public static Diagnostic CreateUnmappedRouteParameter(RestApiMessageDescriptor message, RestApiRouteParameterDescriptor parameter)
        => Diagnostic.Create(UnmappedRouteParameter, Location.None, message.TypeName, parameter.Name);

    public static Diagnostic CreateDuplicateJsonName(RestApiMessageDescriptor message, string jsonName)
        => Diagnostic.Create(DuplicateJsonName, Location.None, message.TypeName, jsonName);

    public static Diagnostic CreateUnmappedQueryBindingRouteParameter(RestApiMessageDescriptor message, string routeParameterName)
        => Diagnostic.Create(UnmappedQueryBindingRouteParameter, Location.None, message.TypeName, routeParameterName);

    public static Diagnostic CreateInvalidQueryBinding(RestApiMessageDescriptor message, string reason)
        => Diagnostic.Create(InvalidQueryBinding, Location.None, message.TypeName, reason);

    public static Diagnostic CreateAmbiguousRoutePropertyMatch(RestApiMessageDescriptor message, string routeParameterName)
        => Diagnostic.Create(AmbiguousRoutePropertyMatch, Location.None, message.TypeName, routeParameterName);
}
