using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Supplies the complete canonical JSON Pointer selection for serializer shapes that cannot be correlated safely.
/// Implements Story 8.1 sections 7 and 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public interface ICanonicalPersonalDataPathPolicy {
    /// <summary>Gets the exact public contract version.</summary>
    int ContractVersion => 1;

    /// <summary>Gets the unique printable ASCII kebab-case policy identifier.</summary>
    string PolicyId { get; }

    /// <summary>Gets the positive policy implementation version.</summary>
    int PolicyVersion { get; }

    /// <summary>Gets the deterministic primary ordering value.</summary>
    int Order { get; }

    /// <summary>Selects the complete canonical JSON Pointer set for the serialized root.</summary>
    /// <param name="identity">The aggregate identity owning the payload.</param>
    /// <param name="serializedRoot">The exact serialized JSON root.</param>
    /// <param name="rootTypeInfo">The source-generated JSON type information for the root.</param>
    /// <param name="payloadKind">The event or snapshot payload kind.</param>
    /// <returns>The complete canonical JSON Pointer selection.</returns>
    IReadOnlyList<string> SelectCanonicalJsonPointers(
        AggregateIdentity identity,
        JsonElement serializedRoot,
        JsonTypeInfo rootTypeInfo,
        PayloadProtectionPayloadKind payloadKind);
}
