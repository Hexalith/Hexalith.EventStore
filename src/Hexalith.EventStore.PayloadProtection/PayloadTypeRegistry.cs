// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Enrolls exact source-generated metadata and explicitly proven CLR/property correlations.</summary>
internal sealed class PayloadTypeRegistry
{
    private readonly Dictionary<Type, JsonTypeInfo> _types = [];
    private readonly Dictionary<JsonPropertyInfo, PropertyInfo> _properties = [];

    /// <summary>Snapshots explicitly supplied metadata; no reflection resolver or assembly scanning is used.</summary>
    internal PayloadTypeRegistry(IEnumerable<JsonTypeInfo> types, IEnumerable<PayloadPropertyRegistration>? properties = null)
    {
        foreach (JsonTypeInfo type in types)
        {
            if (type is null || !_types.TryAdd(type.Type, type))
            {
                throw new PayloadProtectionFormatException();
            }

            // Enrollment is explicit. Freeze the exact metadata so later mutation cannot change discovery shape.
            type.MakeReadOnly();
        }

        foreach (PayloadPropertyRegistration mapping in properties ?? [])
        {
            if (!_types.TryGetValue(mapping.OwnerType, out JsonTypeInfo? owner)
                || !owner.Properties.Contains(mapping.Metadata)
                || mapping.Property.DeclaringType is null
                || !mapping.Property.DeclaringType.IsAssignableFrom(mapping.OwnerType)
                || mapping.Property.PropertyType != mapping.Metadata.PropertyType
                || mapping.Property.GetIndexParameters().Length != 0
                || !_properties.TryAdd(mapping.Metadata, mapping.Property))
            {
                throw new PayloadProtectionFormatException();
            }
        }
    }

    /// <summary>Resolves exact enrolled metadata, failing closed for absent types.</summary>
    internal JsonTypeInfo Resolve(Type type) => _types.TryGetValue(type, out JsonTypeInfo? metadata)
        ? metadata : throw new PayloadProtectionFormatException();

    /// <summary>Obtains a CLR property only from enrolled correlation or serializer-supplied provenance.</summary>
    internal PropertyInfo? Property(JsonPropertyInfo metadata)
        => _properties.TryGetValue(metadata, out PropertyInfo? property) ? property : metadata.AttributeProvider as PropertyInfo;
}
