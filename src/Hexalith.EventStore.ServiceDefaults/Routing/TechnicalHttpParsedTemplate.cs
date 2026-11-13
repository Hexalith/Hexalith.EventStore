using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Privately owns only the last bounded successfully parsed syntax within one technical observation.</summary>
internal sealed class TechnicalHttpParsedTemplate
{
    private const long MaximumRetainedBytes = 1024 * 1024;
    private string? _text;
    private RoutePattern? _pattern;
    private RouteTemplate? _template;

    /// <summary>Gets the retained syntax count; prior variants are never accumulated.</summary>
    internal int Count => _text is null ? 0 : 1;

    /// <summary>Gets conservative owned graph/input storage, including both parsers and private mutable template lists.</summary>
    internal long RetainedBytes { get; private set; }

    /// <summary>Performs the original matching sequence, without exposing parsed models or retaining a match decision.</summary>
    internal bool Matches(string template, string method, string verb, IServiceProvider frameworkServices)
    {
        if (template is not { Length: > 0 and <= 512 } || method is not { Length: > 0 and <= 8192 }) return false;
        try
        {
            var pattern = Pattern(template);
            if (pattern.Parameters.Any(parameter => parameter.IsCatchAll)) return false;
            var values = new RouteValueDictionary();
            var defaults = new RouteValueDictionary(pattern.Defaults.ToDictionary(entry => entry.Key, entry => entry.Value));
            var matcher = new TemplateMatcher(Template(template), defaults);
            if (!matcher.TryMatch(new PathString("/" + method), values)) return false;
            var context = new DefaultHttpContext(); context.Request.Method = verb;
            var resolver = frameworkServices.GetRequiredService<IInlineConstraintResolver>();
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

    private RoutePattern Pattern(string text)
    {
        if (string.Equals(_text, text, StringComparison.Ordinal) && _pattern is not null) return _pattern;
        var current = RoutePatternFactory.Parse(text.TrimStart('/'));
        long nodes = current.PathSegments.Count + current.PathSegments.Sum(segment => segment.Parts.Count)
            + current.Parameters.Count + current.ParameterPolicies.Sum(parameter => parameter.Value.Count) + current.Defaults.Count;
        // Charge well above object/array headers per node plus bounded strings/dictionary storage.
        long charged = 65536 + text.Length * 16L + nodes * 1024;
        if (charged <= MaximumRetainedBytes)
        {
            _text = text; _pattern = current; _template = null; RetainedBytes = charged;
        }
        else Clear();
        return current;
    }

    private RouteTemplate Template(string text)
    {
        if (string.Equals(_text, text, StringComparison.Ordinal) && _template is not null) return _template;
        var current = TemplateParser.Parse(text.TrimStart('/'));
        if (string.Equals(_text, text, StringComparison.Ordinal) && _pattern is not null)
        {
            // The template retains lazy policy enumerables over the second parser graph. Count
            // those without forcing their factories; each fresh TemplateMatcher still runs them.
            long nodes = current.Segments.Count + current.Segments.Sum(segment => segment.Parts.Count)
                + current.Parameters.Count + _pattern.ParameterPolicies.Sum(parameter => parameter.Value.Count);
            long charged = RetainedBytes + nodes * 2048 + text.Length * 16L;
            if (charged <= MaximumRetainedBytes) { _template = current; RetainedBytes = charged; }
            else Clear();
        }
        return current;
    }

    private void Clear() { _text = null; _pattern = null; _template = null; RetainedBytes = 0; }
}
