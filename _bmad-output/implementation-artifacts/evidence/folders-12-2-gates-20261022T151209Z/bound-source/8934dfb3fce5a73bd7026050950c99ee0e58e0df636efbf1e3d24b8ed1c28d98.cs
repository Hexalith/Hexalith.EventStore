namespace Hexalith.EventStore.Client.Projections;

/// <summary>Original immutable envelope bytes retained before legacy compensation removes either copy.</summary>
/// <param name="Scope">The original validated batch scope.</param>
/// <param name="Fingerprint">The original fingerprint, never reconstructed from missing manifest material.</param>
/// <param name="Envelopes">The fully validated original two-key envelope bytes in ordinal order.</param>
internal sealed record ReadModelLegacyScratchCompensation(ReadModelBatchScope Scope, string Fingerprint, byte[][] Envelopes)
{
    /// <summary>Bounds double-base64 evidence independently of the one-MiB original manifest.</summary>
    internal const int MaxEncodedBytes = 4 * ReadModelBatchOptions.DefaultMaxCanonicalManifestBytes + 65_536;
}
