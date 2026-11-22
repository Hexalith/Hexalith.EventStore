using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis.Diagnostics;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

/// <summary>Provides an optional project root namespace.</summary>
internal sealed class GeneratorTestOptions(string? rootNamespace) : AnalyzerConfigOptions
{
    /// <summary>Gets an options set with no root namespace.</summary>
    public static GeneratorTestOptions Empty { get; } = new(null);

    /// <inheritdoc />
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        value = key == "build_property.RootNamespace" ? rootNamespace : null;
        return value is not null;
    }
}
