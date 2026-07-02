using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiMessageParser
{
    private static readonly SymbolDisplayFormat TypeDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

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

        return new RestApiMessageDescriptor(
            GetTypeName(typeSymbol),
            typeSymbol.ToDisplayString(TypeDisplayFormat),
            GetNamespace(typeSymbol),
            typeSymbol.Name,
            isCommand,
            isQuery,
            route,
            GetPublicProperties(typeSymbol));
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

    private static string GetNamespace(INamedTypeSymbol typeSymbol)
        => typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static ImmutableArray<RestApiBindablePropertyDescriptor> GetPublicProperties(INamedTypeSymbol typeSymbol)
    {
        var properties = ImmutableArray.CreateBuilder<RestApiBindablePropertyDescriptor>();
        foreach (IPropertySymbol property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.DeclaredAccessibility != Accessibility.Public
                || property.IsStatic
                || property.IsIndexer
                || property.GetMethod is null)
            {
                continue;
            }

            properties.Add(new RestApiBindablePropertyDescriptor(
                property.Name,
                property.Type.ToDisplayString(TypeDisplayFormat),
                IsSupportedFromQueryType(property.Type)));
        }

        properties.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        return properties.ToImmutable();
    }

    private static bool IsSupportedFromQueryType(ITypeSymbol type)
    {
        ITypeSymbol effectiveType = GetNullableUnderlyingType(type) ?? type;
        if (effectiveType.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (effectiveType.SpecialType is
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_Char or
            SpecialType.System_DateTime or
            SpecialType.System_Decimal or
            SpecialType.System_Double or
            SpecialType.System_Int16 or
            SpecialType.System_Int32 or
            SpecialType.System_Int64 or
            SpecialType.System_SByte or
            SpecialType.System_Single or
            SpecialType.System_String or
            SpecialType.System_UInt16 or
            SpecialType.System_UInt32 or
            SpecialType.System_UInt64)
        {
            return true;
        }

        return string.Equals(effectiveType.ToDisplayString(TypeDisplayFormat), "global::System.Guid", StringComparison.Ordinal)
            || string.Equals(effectiveType.ToDisplayString(TypeDisplayFormat), "global::System.DateTimeOffset", StringComparison.Ordinal);
    }

    private static ITypeSymbol? GetNullableUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } namedType
            && namedType.TypeArguments.Length == 1)
        {
            return namedType.TypeArguments[0];
        }

        return null;
    }
}
