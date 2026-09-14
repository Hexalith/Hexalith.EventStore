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
    int ProtectedPathCount);
