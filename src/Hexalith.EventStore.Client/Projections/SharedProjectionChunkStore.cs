using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Stores immutable, digest-checked chunks under a stable logical identity. Cleanup replaces every
/// slot with a CAS tombstone so a delayed writer cannot recreate confidential payload after cleanup.
/// </summary>
internal sealed class SharedProjectionChunkStore(IReadModelStore store)
{
    internal const int ChunkByteLength = 24 * 1024;
    internal const int MaxPayloadBytes = 64 * 1024 * 1024;
    private const int MaxRetries = 64;

    internal static SharedProjectionChunkReference Describe(string purpose, string identity, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || bytes.Length > MaxPayloadBytes)
        {
            throw new ArgumentException("The shared projection payload is empty or exceeds its bounded chunk limit.", nameof(bytes));
        }

        return new SharedProjectionChunkReference(
            purpose,
            identity,
            Convert.ToHexString(SHA256.HashData(bytes)),
            bytes.Length,
            checked((bytes.Length + ChunkByteLength - 1) / ChunkByteLength));
    }

    internal async Task WriteAsync(
        SharedProjectionScope scope,
        SharedProjectionChunkReference reference,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        Validate(reference);
        if (Describe(reference.Purpose, reference.Identity, bytes) != reference)
        {
            throw new InvalidOperationException("The shared projection chunk bytes conflict with the reserved reference.");
        }

        for (int ordinal = 0; ordinal < reference.ChunkCount; ordinal++)
        {
            byte[] segment = bytes.AsSpan(
                ordinal * ChunkByteLength,
                Math.Min(ChunkByteLength, bytes.Length - ordinal * ChunkByteLength)).ToArray();
            var value = new SharedProjectionChunk(
                reference.Digest,
                Convert.ToHexString(SHA256.HashData(segment)),
                segment,
                false);
            string key = Key(scope, reference, ordinal);
            if (await store.TrySaveAsync(scope.StoreName, key, value, string.Empty, cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            ReadModelEntry<SharedProjectionChunk> existing = await store
                .GetAsync<SharedProjectionChunk>(scope.StoreName, key, cancellationToken).ConfigureAwait(false);
            if (existing.Value is not { Tombstone: false } chunk
                || chunk.RootDigest != value.RootDigest
                || chunk.SegmentDigest != value.SegmentDigest
                || !chunk.Data.AsSpan().SequenceEqual(value.Data))
            {
                throw new InvalidOperationException("The shared projection chunk slot is sealed or conflicts with its reserved bytes.");
            }
        }
    }

    internal async Task<byte[]> ReadAsync(
        SharedProjectionScope scope,
        SharedProjectionChunkReference reference,
        CancellationToken cancellationToken)
    {
        Validate(reference);
        byte[] bytes = new byte[reference.ByteLength];
        for (int ordinal = 0; ordinal < reference.ChunkCount; ordinal++)
        {
            ReadModelEntry<SharedProjectionChunk> entry = await store
                .GetAsync<SharedProjectionChunk>(scope.StoreName, Key(scope, reference, ordinal), cancellationToken)
                .ConfigureAwait(false);
            int expectedLength = Math.Min(ChunkByteLength, reference.ByteLength - ordinal * ChunkByteLength);
            if (entry.Value is not { Tombstone: false } chunk
                || chunk.RootDigest != reference.Digest
                || chunk.Data is null
                || chunk.Data.Length != expectedLength
                || chunk.SegmentDigest != Convert.ToHexString(SHA256.HashData(chunk.Data)))
            {
                throw new InvalidOperationException("The shared projection chunk is absent or corrupt.");
            }

            chunk.Data.CopyTo(bytes, ordinal * ChunkByteLength);
        }

        if (Convert.ToHexString(SHA256.HashData(bytes)) != reference.Digest)
        {
            throw new InvalidOperationException("The shared projection payload digest does not match its CAS reference.");
        }

        return bytes;
    }

    internal async Task TombstoneAsync(
        SharedProjectionScope scope,
        SharedProjectionChunkReference reference,
        CancellationToken cancellationToken)
    {
        Validate(reference);
        for (int ordinal = 0; ordinal < reference.ChunkCount; ordinal++)
        {
            string key = Key(scope, reference, ordinal);
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                ReadModelEntry<SharedProjectionChunk> entry = await store
                    .GetAsync<SharedProjectionChunk>(scope.StoreName, key, cancellationToken).ConfigureAwait(false);
                if (entry.Value is { } existing && existing.RootDigest != reference.Digest)
                {
                    throw new InvalidOperationException("The shared projection chunk cleanup conflicts with another payload.");
                }

                if (entry.Value?.Tombstone == true)
                {
                    break;
                }

                var tombstone = new SharedProjectionChunk(reference.Digest, string.Empty, [], true);
                if (await store.TrySaveAsync(scope.StoreName, key, tombstone, entry.ETag ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false))
                {
                    break;
                }

                if (attempt == MaxRetries - 1)
                {
                    throw new InvalidOperationException("The shared projection chunk cleanup CAS retry limit was exhausted.");
                }
            }
        }
    }

    internal static string Key(SharedProjectionScope scope, SharedProjectionChunkReference reference, int ordinal)
    {
        byte[] identity = Encoding.UTF8.GetBytes(reference.Purpose + "\0" + reference.Identity);
        return "shared-chunk:" + scope.ComputeHash() + ":" + Convert.ToHexString(SHA256.HashData(identity))
            + ":" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Validate(SharedProjectionChunkReference reference)
    {
        if (reference is null
            || string.IsNullOrWhiteSpace(reference.Purpose)
            || string.IsNullOrWhiteSpace(reference.Identity)
            || reference.Digest is null
            || reference.Digest.Length != 64
            || reference.ByteLength <= 0
            || reference.ByteLength > MaxPayloadBytes
            || reference.ChunkCount != (reference.ByteLength + ChunkByteLength - 1) / ChunkByteLength)
        {
            throw new InvalidOperationException("The shared projection chunk reference is invalid.");
        }
    }
}
