using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Constructs the canonical protected-path manifest from normative section 7.2.
/// </summary>
internal static class ProtectedPathManifestCodec
{
    /// <summary>
    /// Snapshots, validates, sorts, and encodes a complete path set with bounded enumeration and cancellation.
    /// </summary>
    internal static ProtectedPathManifest Create(
        IEnumerable<string> paths,
        bool snapshot = false,
        CancellationToken cancellationToken = default,
        Action<int>? checkpoint = null,
        Action<int>? sortCheckpoint = null,
        Action<int>? hashCheckpoint = null,
        bool allowEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var pathValues = new List<string>();
        var encodedPaths = new List<byte[]>();
        byte[]? encoded = null;
        byte[]? commitment = null;
        long totalLength = 9;
        try
        {
            foreach (string path in paths)
            {
                int count = checked(pathValues.Count + 1);
                if (count == 1 || (count & 255) == 0)
                {
                    checkpoint?.Invoke(count);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (count > PayloadProtectionLimits.ProtectedPaths)
                {
                    throw new PayloadProtectionFormatException();
                }

                if (path is null)
                {
                    throw new PayloadProtectionFormatException();
                }

                _ = pathValues.EnsureCapacity(count);
                _ = encodedPaths.EnsureCapacity(count);
                string pathSnapshot = new(path.AsSpan());
                _ = JsonPointer.Decode(pathSnapshot, allowRoot: snapshot);
                if (snapshot != (pathSnapshot.Length == 0))
                {
                    throw new PayloadProtectionFormatException();
                }

                byte[] encodedPath = CanonicalText.Encode(
                    pathSnapshot,
                    snapshot ? 0 : 1,
                    PayloadProtectionLimits.PathBytes);
                totalLength = checked(totalLength + 4 + encodedPath.Length);
                if (totalLength > PayloadProtectionLimits.ManifestBytes)
                {
                    CryptographicOperations.ZeroMemory(encodedPath);
                    throw new PayloadProtectionFormatException();
                }

                pathValues.Add(pathSnapshot);
                encodedPaths.Add(encodedPath);
            }

            if ((!allowEmpty && pathValues.Count == 0)
                || (snapshot && pathValues.Count != 1))
            {
                throw new PayloadProtectionFormatException();
            }

            cancellationToken.ThrowIfCancellationRequested();
            int[] order = new int[pathValues.Count];
            for (int index = 0; index < order.Length; index++)
            {
                order[index] = index;
            }

            int comparisons = 0;
            Array.Sort(order, (left, right) =>
            {
                comparisons = checked(comparisons + 1);
                if (comparisons == 1 || (comparisons & 255) == 0)
                {
                    sortCheckpoint?.Invoke(comparisons);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return Compare(encodedPaths[left], encodedPaths[right]);
            });
            cancellationToken.ThrowIfCancellationRequested();
            string[] sortedPaths = new string[order.Length];
            byte[][] sortedEncodedPaths = new byte[order.Length][];
            for (int index = 0; index < order.Length; index++)
            {
                if (index == 0 || (index & 255) == 0)
                {
                    checkpoint?.Invoke(index + 1);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                sortedPaths[index] = pathValues[order[index]];
                sortedEncodedPaths[index] = encodedPaths[order[index]];
                if (index > 0)
                {
                    ReadOnlySpan<byte> previous = sortedEncodedPaths[index - 1];
                    ReadOnlySpan<byte> current = sortedEncodedPaths[index];
                    if (current.SequenceEqual(previous)
                        || (current.Length > previous.Length
                            && current[..previous.Length].SequenceEqual(previous)
                            && current[previous.Length] == (byte)'/'))
                    {
                        throw new PayloadProtectionFormatException();
                    }
                }
            }

            encoded = new byte[checked((int)totalLength)];
            "HXPM"u8.CopyTo(encoded);
            encoded[4] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(5), checked((uint)sortedPaths.Length));
            int offset = 9;
            for (int index = 0; index < sortedEncodedPaths.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] pathBytes = sortedEncodedPaths[index];
                BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(offset), checked((uint)pathBytes.Length));
                offset += 4;
                pathBytes.CopyTo(encoded, offset);
                offset += pathBytes.Length;
            }

            cancellationToken.ThrowIfCancellationRequested();
            commitment = HashWithCancellation(encoded, cancellationToken, hashCheckpoint);
            cancellationToken.ThrowIfCancellationRequested();
            var result = new ProtectedPathManifest(
                new ReadOnlyCollection<string>(sortedPaths),
                encoded,
                commitment);
            encoded = null;
            commitment = null;
            return result;
        }
        catch (OverflowException)
        {
            throw new PayloadProtectionFormatException();
        }
        finally
        {
            for (int index = 0; index < encodedPaths.Count; index++)
            {
                CryptographicOperations.ZeroMemory(encodedPaths[index]);
            }

            if (encoded is not null)
            {
                CryptographicOperations.ZeroMemory(encoded);
            }

            if (commitment is not null)
            {
                CryptographicOperations.ZeroMemory(commitment);
            }
        }
    }

    private static byte[] HashWithCancellation(
        ReadOnlySpan<byte> encoded,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int processed = 0;
        while (processed < encoded.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = Math.Min(4096, encoded.Length - processed);
            hash.AppendData(encoded.Slice(processed, length));
            processed += length;
            checkpoint?.Invoke(processed);
            cancellationToken.ThrowIfCancellationRequested();
        }

        byte[]? result = hash.GetHashAndReset();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] transferred = result;
            result = null;
            return transferred;
        }
        finally
        {
            if (result is not null)
            {
                CryptographicOperations.ZeroMemory(result);
            }
        }
    }

    private static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        int common = Math.Min(left.Length, right.Length);
        for (int index = 0; index < common; index++)
        {
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
