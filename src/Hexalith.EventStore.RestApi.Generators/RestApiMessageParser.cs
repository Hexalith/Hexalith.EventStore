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

        string? unsupportedReason = GetUnsupportedContractReason(typeSymbol);
        INamedTypeSymbol? routeAttribute = compilation.GetTypeByMetadataName(RestApiMetadataNames.RestRouteAttribute);
        INamedTypeSymbol? jsonPropertyNameAttribute = compilation.GetTypeByMetadataName(RestApiMetadataNames.JsonPropertyNameAttribute);
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
            unsupportedReason is null
                ? GetPublicProperties(typeSymbol, jsonPropertyNameAttribute)
                : ImmutableArray<RestApiBindablePropertyDescriptor>.Empty,
            unsupportedReason);
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

    private static string? GetUnsupportedContractReason(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.IsAbstract)
        {
            return "it is abstract";
        }

        if (typeSymbol.Arity != 0)
        {
            return "it is generic";
        }

        for (INamedTypeSymbol? current = typeSymbol.ContainingType; current is not null; current = current.ContainingType)
        {
            if (current.Arity != 0)
            {
                return "it is nested in a generic type";
            }
        }

        for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return "it is not publicly accessible";
            }
        }

        return null;
    }

    private static ImmutableArray<RestApiBindablePropertyDescriptor> GetPublicProperties(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol? jsonPropertyNameAttribute)
    {
        var properties = ImmutableArray.CreateBuilder<RestApiBindablePropertyDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = typeSymbol; current is not null; current = current.BaseType)
        {
            foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility != Accessibility.Public
                    || property.IsStatic
                    || property.IsIndexer
                    || property.GetMethod is null
                    || !seen.Add(property.Name))
                {
                    continue;
                }

                properties.Add(new RestApiBindablePropertyDescriptor(
                    property.Name,
                    GetJsonPropertyName(property, jsonPropertyNameAttribute),
                    property.Type.ToDisplayString(TypeDisplayFormat),
                    IsSupportedFromQueryType(property.Type)));
            }
        }

        properties.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
        return properties.ToImmutable();
    }

    private static string GetJsonPropertyName(IPropertySymbol property, INamedTypeSymbol? jsonPropertyNameAttribute)
    {
        if (jsonPropertyNameAttribute is not null)
        {
            foreach (AttributeData attribute in property.GetAttributes())
            {
                if (RoslynAttributeValueReader.IsAttribute(attribute, jsonPropertyNameAttribute)
                    && attribute.ConstructorArguments.Length > 0)
                {
                    string name = RoslynAttributeValueReader.GetString(attribute.ConstructorArguments[0]);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
        }

        return ToJsonCamelCase(property.Name);
    }

    private static string ToJsonCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return name;
        }

        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            bool hasNext = i + 1 < chars.Length;
            if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
            {
                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
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
