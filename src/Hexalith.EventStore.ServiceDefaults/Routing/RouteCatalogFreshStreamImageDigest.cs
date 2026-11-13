using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Reads the complete original stream once, bounding captured bytes without treating length hints as EOF evidence.</summary>
internal static class RouteCatalogFreshStreamImageDigest
{
    private const int ReadChunkBytes = 64 * 1024;
    private static readonly ArrayPool<byte> Buffers = ArrayPool<byte>.Create(RouteCatalogImageDigestMemo.MaximumImageBytes, 1);

    /// <summary>Preserves ordinary streaming SHA outside an open private scope, otherwise reuses only complete fresh-byte equality.</summary>
    internal static string Compute(Assembly assembly, Stream image) => Compute(assembly, image, RouteCatalogImageBuffers.Select(Buffers));

    /// <summary>Owns every borrowed buffer through complete EOF, comparison and hashing, including growth and failures.</summary>
    internal static string Compute(Assembly assembly, Stream image, ArrayPool<byte> buffers)
    {
        if (RouteCatalogImageDigestScope.Current is not { IsOpen: true } scope)
            return Convert.ToHexStringLower(SHA256.HashData(image));
        int requested = InitialCapacity(image);
        if (requested == 0) return Convert.ToHexStringLower(SHA256.HashData(image));
        byte[]? bytes = null;
        try
        {
            bytes = buffers.Rent(requested);
            int count = 0;
            while (true)
            {
                int limit = Math.Min(bytes.Length, RouteCatalogImageDigestMemo.MaximumImageBytes);
                if (count == limit)
                {
                    // A full allocation is never proof of EOF. Consume the next original byte before growing or falling back.
                    int next = image.ReadByte();
                    if (next < 0) return Complete(scope, assembly, bytes.AsSpan(0, count));
                    if (limit == RouteCatalogImageDigestMemo.MaximumImageBytes)
                        return HashOversizedRemainder(image, bytes, count, (byte)next);
                    byte[] larger = buffers.Rent(Math.Min(RouteCatalogImageDigestMemo.MaximumImageBytes, limit * 2));
                    bytes.AsSpan(0, count).CopyTo(larger);
                    buffers.Return(bytes);
                    bytes = larger;
                    bytes[count++] = (byte)next;
                }
                int read = image.Read(bytes.AsSpan(count, Math.Min(ReadChunkBytes,
                    Math.Min(bytes.Length, RouteCatalogImageDigestMemo.MaximumImageBytes) - count)));
                if (read == 0) return Complete(scope, assembly, bytes.AsSpan(0, count));
                count += read;
            }
        }
        finally { if (bytes is not null) buffers.Return(bytes); }
    }

    private static int InitialCapacity(Stream image)
    {
        try
        {
            if (image.CanSeek)
            {
                long remaining = image.Length - image.Position;
                if (remaining > RouteCatalogImageDigestMemo.MaximumImageBytes) return 0;
                return (int)Math.Clamp(remaining, 4096, RouteCatalogImageDigestMemo.MaximumImageBytes);
            }
        }
        catch (NotSupportedException) { /* Allocation hints never substitute for the original stream read. */ }
        catch (IOException) { /* A failed optional hint keeps the ordinary read semantics. */ }
        return ReadChunkBytes;
    }

    private static string Complete(RouteCatalogImageDigestScope scope, Assembly assembly, ReadOnlySpan<byte> current)
        => scope.TryCompute(assembly, current, out string digest) ? digest : Convert.ToHexStringLower(SHA256.HashData(current));

    private static string HashOversizedRemainder(Stream image, byte[] bytes, int count, byte next)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(bytes.AsSpan(0, count));
        hash.AppendData([next]);
        int read;
        while ((read = image.Read(bytes.AsSpan(0, Math.Min(ReadChunkBytes, bytes.Length)))) != 0)
            hash.AppendData(bytes.AsSpan(0, read));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
