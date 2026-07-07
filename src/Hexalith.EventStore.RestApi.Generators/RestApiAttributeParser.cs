using Microsoft.CodeAnalysis;

namespace Hexalith.EventStore.RestApi.Generators;

internal static class RestApiAttributeParser
{
    public static RestApiOptions Parse(Compilation compilation, CancellationToken cancellationToken)
    {
        INamedTypeSymbol? attributeSymbol = compilation.GetTypeByMetadataName(RestApiMetadataNames.RestApiAttribute);
        if (attributeSymbol is null)
        {
            return new RestApiOptions(false, string.Empty, string.Empty, "Claims");
        }

        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!RoslynAttributeValueReader.IsAttribute(attribute, attributeSymbol))
            {
                continue;
            }

            string routePrefix = attribute.ConstructorArguments.Length > 0
                ? RoslynAttributeValueReader.GetString(attribute.ConstructorArguments[0])
                : string.Empty;
            string tag = attribute.ConstructorArguments.Length > 1
                ? NormalizeOptionalText(RoslynAttributeValueReader.GetString(attribute.ConstructorArguments[1]))
                : string.Empty;
            string tenantSource = attribute.ConstructorArguments.Length > 2
                ? RoslynAttributeValueReader.GetEnumName(attribute.ConstructorArguments[2], "Claims")
                : "Claims";

            return new RestApiOptions(true, routePrefix, tag, tenantSource);
        }

        return new RestApiOptions(false, string.Empty, string.Empty, "Claims");
    }

    private static string NormalizeOptionalText(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
