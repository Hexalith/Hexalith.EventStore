using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiNamespaceResolver
{
    public static string Resolve(Compilation compilation, AnalyzerConfigOptionsProvider optionsProvider)
    {
        string? rootNamespace = null;
        _ = optionsProvider.GlobalOptions.TryGetValue("build_property.RootNamespace", out rootNamespace);
        string fallback = compilation.AssemblyName ?? "RestApi";
        string resolved = string.IsNullOrWhiteSpace(rootNamespace) ? fallback : rootNamespace!;
        return RestApiNameSanitizer.ToNamespace(resolved, "RestApi") + ".Generated";
    }
}
