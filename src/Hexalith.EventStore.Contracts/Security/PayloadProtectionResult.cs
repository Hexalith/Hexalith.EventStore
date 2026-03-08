namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Represents transformed payload bytes after optional protection or unprotection.
/// </summary>
/// <param name="PayloadBytes">The transformed payload bytes.</param>
/// <param name="SerializationFormat">The serialization format associated with the transformed payload.</param>
public sealed record PayloadProtectionResult(byte[] PayloadBytes, string SerializationFormat);