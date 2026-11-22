using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hexalith.EventStore.RestApi.Generators.Tests;

/// <summary>Provides global analyzer options to the generator test driver.</summary>
internal sealed class GeneratorTestOptionsProvider(string rootNamespace) : AnalyzerConfigOptionsProvider
{
    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions { get; } = new GeneratorTestOptions(rootNamespace);

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GeneratorTestOptions.Empty;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GeneratorTestOptions.Empty;
}
