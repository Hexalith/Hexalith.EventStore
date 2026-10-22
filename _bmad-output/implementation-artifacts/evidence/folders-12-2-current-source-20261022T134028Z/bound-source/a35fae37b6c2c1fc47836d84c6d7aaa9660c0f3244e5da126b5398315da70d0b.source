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

        IncrementalValueProvider<string> controllerNamespace = context.CompilationProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (source, _) => RestApiNamespaceResolver.Resolve(source.Left, source.Right))
            .WithTrackingName("RestApiControllerNamespace");

        IncrementalValueProvider<ImmutableArray<RestApiMessageDescriptor>> collectedMessages = messages
            .Collect()
            .Select(static (descriptors, _) => descriptors.Distinct().ToImmutableArray())
            .WithComparer(RestApiMessageDescriptorArrayComparer.Instance)
            .WithTrackingName("RestApiMessageDeduplication");

        IncrementalValueProvider<ImmutableArray<RestApiMessageDescriptor>> referencedMessages = context.CompilationProvider
            .Combine(restApiOptions)
            .Select(static (source, cancellationToken) => RestApiMessageParser.ParseReferenced(
                source.Left,
                source.Right,
                cancellationToken))
            .WithComparer(RestApiMessageDescriptorArrayComparer.Instance)
            .WithTrackingName("RestApiReferencedMessageDiscovery");

        IncrementalValueProvider<ImmutableArray<RestApiMessageDescriptor>> allMessages = collectedMessages
            .Combine(referencedMessages)
            .Select(static (source, _) => source.Left.AddRange(source.Right).Distinct().ToImmutableArray())
            .WithComparer(RestApiMessageDescriptorArrayComparer.Instance)
            .WithTrackingName("RestApiAllMessageDeduplication");

        context.RegisterSourceOutput(
            restApiOptions.Combine(allMessages).Combine(controllerNamespace),
            static (sourceProductionContext, source) =>
            {
                RestApiOptions options = source.Left.Left;
                if (!options.Found)
                {
                    return;
                }

                ImmutableArray<RestApiMessageDescriptor> sourceMessages = source.Left.Right;
                string manifest = RestApiManifestEmitter.Emit(options, sourceMessages);
                sourceProductionContext.AddSource(
                    RestApiManifestEmitter.HintName,
                    SourceText.From(manifest, Encoding.UTF8));

                RestApiGeneratedSource? controller = RestApiControllerEmitter.Emit(
                    options,
                    source.Right,
                    sourceMessages,
                    diagnostic => sourceProductionContext.ReportDiagnostic(diagnostic));
                if (controller.HasValue)
                {
                    sourceProductionContext.AddSource(
                        controller.Value.HintName,
                        SourceText.From(controller.Value.Source, Encoding.UTF8));
                }
            });
    }
}
