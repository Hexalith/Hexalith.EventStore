// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>An explicit serializer metadata to CLR-property correlation supplied by trusted enrollment.</summary>
/// <param name="OwnerType">The enrolled declaring payload type.</param>
/// <param name="Metadata">The exact serializer property metadata.</param>
/// <param name="Property">The proven corresponding CLR property.</param>
internal sealed record PayloadPropertyRegistration(Type OwnerType, JsonPropertyInfo Metadata, PropertyInfo Property);
