using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Field envelope for content owned by one explicit interaction root, including directory creation outboxes.</summary>
/// <param name="Target">Complete tenant, interaction and alias identity; never inferred from the directory aggregate.</param>
/// <param name="Value">The content value, a sealed v2 wrapper in storage, or a typed <see cref="Erased"/> marker on replay.</param>
public sealed record ProtectedContent(
    [property: JsonPropertyName("Target")] ProtectionTarget Target,
    [property: JsonPropertyName("Value")] JsonElement Value)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(ProtectedContent);
}
