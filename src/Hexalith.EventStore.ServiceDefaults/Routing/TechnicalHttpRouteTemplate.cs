using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Uses the framework's full template and built-in constraint semantics for independently qualified technical actions.</summary>
internal static class TechnicalHttpRouteTemplate
{
    private static readonly IServiceProvider FrameworkServices = new ServiceCollection().AddRouting().BuildServiceProvider();

    internal static object[] ObserveConstraints(RouteEndpoint endpoint, IServiceProvider services, TechnicalHttpObservationPass pass)
    {
        var resolver = services.GetRequiredService<IInlineConstraintResolver>();
        var installed = services.GetRequiredService<IOptions<RouteOptions>>().Value.ConstraintMap;
        var framework = FrameworkServices.GetRequiredService<IOptions<RouteOptions>>().Value.ConstraintMap;
        if (resolver.GetType() != typeof(DefaultInlineConstraintResolver))
            throw new RouteCatalogUnavailableException("technical-http-custom-constraint-resolver");
        List<object> constraints = [];
        foreach (var parameter in endpoint.RoutePattern.ParameterPolicies.OrderBy(parameter => parameter.Key, StringComparer.Ordinal))
        foreach (var reference in parameter.Value)
        {
            string content = reference.Content ?? throw new RouteCatalogUnavailableException("technical-http-unbound-route-constraint");
            string name = content.Split('(', 2)[0];
            if (reference.ParameterPolicy is not null || !framework.TryGetValue(name, out var expected)
                || !installed.TryGetValue(name, out var actual) || actual != expected)
                throw new RouteCatalogUnavailableException("technical-http-replaced-route-constraint");
            var constraint = resolver.ResolveConstraint(content);
            if (constraint is null || constraint.GetType() != expected)
                throw new RouteCatalogUnavailableException("technical-http-replaced-route-constraint");
            constraints.Add(new { parameter = parameter.Key, content, type = constraint.GetType().FullName,
                code = pass.AssemblyIdentity(constraint.GetType().Assembly) });
        }
        return constraints.ToArray();
    }

    internal static bool Matches(string template, string method, string verb)
    {
        if (template is not { Length: > 0 and <= 512 } || method is not { Length: > 0 and <= 8192 }) return false;
        try
        {
            var pattern = RoutePatternFactory.Parse(template.TrimStart('/'));
            if (pattern.Parameters.Any(parameter => parameter.IsCatchAll)) return false;
            var values = new RouteValueDictionary();
            var defaults = new RouteValueDictionary(pattern.Defaults.ToDictionary(entry => entry.Key, entry => entry.Value));
            var matcher = new TemplateMatcher(TemplateParser.Parse(template.TrimStart('/')), defaults);
            if (!matcher.TryMatch(new PathString("/" + method), values)) return false;
            var context = new DefaultHttpContext(); context.Request.Method = verb;
            var resolver = FrameworkServices.GetRequiredService<IInlineConstraintResolver>();
            foreach (var parameter in pattern.ParameterPolicies)
            {
                if (!values.ContainsKey(parameter.Key) && pattern.Parameters.Single(value => value.Name == parameter.Key).IsOptional) continue;
                foreach (var reference in parameter.Value)
                    if (reference.Content is not { } content || resolver.ResolveConstraint(content) is not { } constraint
                        || !constraint.Match(context, route: null, parameter.Key, values, RouteDirection.IncomingRequest)) return false;
            }
            return true;
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException) { return false; }
    }
}
