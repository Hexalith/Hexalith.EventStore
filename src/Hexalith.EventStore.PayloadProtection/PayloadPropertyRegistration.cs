// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>An explicit serializer metadata to CLR-property correlation supplied by trusted enrollment.</summary>
/// <param name="OwnerType">The enrolled declaring payload type.</param>
/// <param name="Metadata">The exact serializer property metadata.</param>
/// <param name="Property">The proven corresponding CLR property.</param>
internal sealed record PayloadPropertyRegistration(Type OwnerType, JsonPropertyInfo Metadata, PropertyInfo Property);
