using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Hexalith.EventStore.RestApi.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class RestApiGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<RestApiMessageDescriptor> messages = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, cancellationToken) => RestApiMessageParser.IsCandidate(node, cancellationToken),
                transform: static (syntaxContext, cancellationToken) => RestApiMessageParser.Parse(syntaxContext, cancellationToken))
            .Where(static descriptor => descriptor.HasValue)
            .Select(static (descriptor, _) => descriptor.GetValueOrDefault())
            .WithTrackingName("RestApiMessageDiscovery");

        IncrementalValueProvider<RestApiOptions> restApiOptions = context.CompilationProvider
            .Select(static (compilation, cancellationToken) => RestApiAttributeParser.Parse(compilation, cancellationToken))
            .WithTrackingName("RestApiOptions");

        IncrementalValueProvider<ImmutableArray<RestApiMessageDescriptor>> collectedMessages = messages.Collect();

        context.RegisterSourceOutput(
            restApiOptions.Combine(collectedMessages),
            static (sourceProductionContext, source) =>
            {
                RestApiOptions options = source.Left;
                if (!options.Found)
                {
                    return;
                }

                string manifest = RestApiManifestEmitter.Emit(options, source.Right);
                sourceProductionContext.AddSource(
                    RestApiManifestEmitter.HintName,
                    SourceText.From(manifest, Encoding.UTF8));
            });
    }
}
