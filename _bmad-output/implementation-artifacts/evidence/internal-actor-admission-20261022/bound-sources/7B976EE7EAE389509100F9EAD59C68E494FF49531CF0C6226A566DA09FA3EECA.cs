using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Supplies the exact serialized value and its typed provenance to a personal-data policy.
/// Implements Story 8.1 sections 7 and 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// Policies must not retain this call-scoped context or any referenced object.
/// </summary>
/// <param name="Identity">The aggregate identity owning the payload.</param>
/// <param name="Root">The typed root event or snapshot object.</param>
/// <param name="SerializedRoot">The exact serialized JSON root.</param>
/// <param name="RootTypeInfo">The source-generated JSON type information for the root.</param>
/// <param name="Owner">The object owning <paramref name="Property"/>, or <see langword="null"/> when unavailable.</param>
/// <param name="Property">The correlated CLR property, or <see langword="null"/> when unavailable.</param>
/// <param name="SerializedValue">The exact serialized JSON value under evaluation.</param>
/// <param name="JsonPointer">The canonical JSON Pointer identifying the serialized value.</param>
/// <param name="PayloadTypeName">The persisted event type or registered stable snapshot type identifier.</param>
/// <param name="PayloadKind">The event or snapshot payload kind.</param>
public sealed record PersonalDataPolicyContext(
    AggregateIdentity Identity,
    object Root,
    JsonElement SerializedRoot,
    JsonTypeInfo RootTypeInfo,
    object? Owner,
    PropertyInfo? Property,
    JsonElement SerializedValue,
    string JsonPointer,
    string PayloadTypeName,
    PayloadProtectionPayloadKind PayloadKind) {
    /// <summary>Returns a bounded diagnostic name without payload, path, type, or identity data.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(PersonalDataPolicyContext);
}
