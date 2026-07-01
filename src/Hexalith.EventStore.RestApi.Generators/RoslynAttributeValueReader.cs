using Microsoft.CodeAnalysis;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RoslynAttributeValueReader
{
    public static bool IsAttribute(AttributeData attribute, INamedTypeSymbol attributeSymbol)
        => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol);

    public static string GetString(TypedConstant typedConstant)
        => typedConstant.Value as string ?? string.Empty;

    public static string GetEnumName(TypedConstant typedConstant, string defaultName)
    {
        object? value = typedConstant.Value;
        if (value is null || typedConstant.Type is not INamedTypeSymbol enumType)
        {
            return defaultName;
        }

        foreach (ISymbol member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field
                && Equals(field.ConstantValue, value))
            {
                return field.Name;
            }
        }

        return value.ToString() ?? defaultName;
    }
}
