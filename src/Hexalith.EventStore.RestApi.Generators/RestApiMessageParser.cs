using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiMessageParser
{
    public static bool IsCandidate(SyntaxNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return node is TypeDeclarationSyntax { BaseList: not null } typeDeclaration
            && (typeDeclaration is ClassDeclarationSyntax || typeDeclaration is RecordDeclarationSyntax);
    }

    public static RestApiMessageDescriptor? Parse(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Node is not TypeDeclarationSyntax typeDeclaration
            || context.SemanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not INamedTypeSymbol typeSymbol
            || typeSymbol.TypeKind != TypeKind.Class)
        {
            return null;
        }

        Compilation compilation = context.SemanticModel.Compilation;
        INamedTypeSymbol? commandContract = compilation.GetTypeByMetadataName(RestApiMetadataNames.CommandContract);
        INamedTypeSymbol? queryContract = compilation.GetTypeByMetadataName(RestApiMetadataNames.QueryContract);
        bool isCommand = commandContract is not null && Implements(typeSymbol, commandContract);
        bool isQuery = queryContract is not null && Implements(typeSymbol, queryContract);
        if (!isCommand && !isQuery)
        {
            return null;
        }

        INamedTypeSymbol? routeAttribute = compilation.GetTypeByMetadataName(RestApiMetadataNames.RestRouteAttribute);
        RestApiRouteDescriptor? route = routeAttribute is null
            ? null
            : ParseRoute(typeSymbol, routeAttribute);

        return new RestApiMessageDescriptor(GetTypeName(typeSymbol), isCommand, isQuery, route);
    }

    private static bool Implements(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
    {
        foreach (INamedTypeSymbol implemented in typeSymbol.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(implemented, interfaceSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static RestApiRouteDescriptor? ParseRoute(INamedTypeSymbol typeSymbol, INamedTypeSymbol routeAttribute)
    {
        foreach (AttributeData attribute in typeSymbol.GetAttributes())
        {
            if (!RoslynAttributeValueReader.IsAttribute(attribute, routeAttribute))
            {
                continue;
            }

            string verb = attribute.ConstructorArguments.Length > 0
                ? RoslynAttributeValueReader.GetEnumName(attribute.ConstructorArguments[0], string.Empty)
                : string.Empty;
            string template = attribute.ConstructorArguments.Length > 1
                ? RoslynAttributeValueReader.GetString(attribute.ConstructorArguments[1])
                : string.Empty;

            return new RestApiRouteDescriptor(verb, template);
        }

        return null;
    }

    private static string GetTypeName(INamedTypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
}
