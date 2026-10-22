namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Identifies the exact durable parts of one mapped full replay request.</summary>
/// <param name="PartCount">Number of bounded request parts.</param>
/// <param name="Digest">SHA-256 digest of the complete serialized event array.</param>
public sealed record ProjectionRebuildTransportReference(int PartCount, string Digest)
{
    /// <summary>Gets the maximum raw bytes in one wire part.</summary>
    public const int PartBytes = 256 * 1024;

    /// <summary>Gets the maximum durable parts in one mapped replay.</summary>
    public const int MaximumParts = 262_144;
}
