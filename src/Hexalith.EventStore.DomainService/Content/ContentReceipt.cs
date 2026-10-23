namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Immutable server-verified metadata; receipt possession grants no authority.</summary>
/// <param name="Id">Unpredictable lowercase opaque upload identity.</param>
/// <param name="Sha256">Lowercase SHA-256 of exact bytes; adapters may expose hashref_ followed by this value.</param>
/// <param name="ByteLength">Verified exact byte length.</param>
/// <param name="MediaType">Validated media type.</param>
/// <param name="TaskId">Immutable assigned task or null; lifecycle owners use this for orphan terminal proof.</param>
public sealed record ContentReceipt(string Id, string Sha256, long ByteLength, string MediaType, string? TaskId = null);
