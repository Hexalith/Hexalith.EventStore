// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 5-8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Carries a complete transformed event payload or the original unselected payload.
/// </summary>
/// <param name="PayloadBytes">The complete output bytes.</param>
/// <param name="SerializationFormat">The output serialization format.</param>
/// <param name="ProtectedPathCount">The number of protected non-null values.</param>
internal sealed record CoreProtectionResult(
    byte[] PayloadBytes,
    string SerializationFormat,
    int ProtectedPathCount)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(CoreProtectionResult);
}
