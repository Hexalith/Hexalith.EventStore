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
        Action<int>? encodingCheckpoint = null,
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
            IEnumerator<string> enumerator;
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                enumerator = paths.GetEnumerator();
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            using (enumerator)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (MoveNext(enumerator, cancellationToken))
                {
                    string path = ReadCurrent(enumerator, cancellationToken);
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

                    if (path is null || path.Length > PayloadProtectionLimits.PathBytes)
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
            }

            cancellationToken.ThrowIfCancellationRequested();

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
            try
            {
                Array.Sort(order, (left, right) =>
                {
                    comparisons = checked(comparisons + 1);
                    if (comparisons == 1 || (comparisons & 255) == 0)
                    {
                        sortCheckpoint?.Invoke(comparisons);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    return Compare(
                        encodedPaths[left],
                        encodedPaths[right],
                        cancellationToken,
                        sortCheckpoint);
                });
            }
            catch (InvalidOperationException exception)
                when (cancellationToken.IsCancellationRequested
                    && exception.InnerException is OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string[] sortedPaths = new string[order.Length];
            byte[][] sortedEncodedPaths = new byte[order.Length][];
            int copiedPaths = 0;
            for (int index = 0; index < order.Length; index++)
            {
                CheckCancellation(ref copiedPaths, cancellationToken, checkpoint);

                sortedPaths[index] = pathValues[order[index]];
                sortedEncodedPaths[index] = encodedPaths[order[index]];
            }

            ValidateOverlap(sortedEncodedPaths, cancellationToken, checkpoint);

            encoded = new byte[checked((int)totalLength)];
            "HXPM"u8.CopyTo(encoded);
            encoded[4] = 1;
            BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(5), checked((uint)sortedPaths.Length));
            int offset = 9;
            int encodedPathCount = 0;
            for (int index = 0; index < sortedEncodedPaths.Length; index++)
            {
                CheckCancellation(ref encodedPathCount, cancellationToken, encodingCheckpoint);

                byte[] pathBytes = sortedEncodedPaths[index];
                BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(offset), checked((uint)pathBytes.Length));
                offset += 4;
                CopyWithCancellation(
                    pathBytes,
                    encoded.AsSpan(offset),
                    cancellationToken,
                    encodingCheckpoint);
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

    private static bool MoveNext(IEnumerator<string> enumerator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            bool result = enumerator.MoveNext();
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static string ReadCurrent(IEnumerator<string> enumerator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string result = enumerator.Current;
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static byte[] HashWithCancellation(
        ReadOnlySpan<byte> encoded,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int processed = 0;
        checkpoint?.Invoke(1);
        cancellationToken.ThrowIfCancellationRequested();
        while (processed < encoded.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = Math.Min(256, encoded.Length - processed);
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

    private static int Compare(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        int common = Math.Min(left.Length, right.Length);
        for (int index = 0; index < common; index++)
        {
            if (index == 0 || (index & 255) == 0)
            {
                checkpoint?.Invoke(index + 1);
                cancellationToken.ThrowIfCancellationRequested();
            }

            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static void ValidateOverlap(
        IReadOnlyList<byte[]> paths,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        int examinedBytes = 0;
        byte[][] descendantPrefixes = new byte[paths.Count][];
        try
        {
            int prefixCount = 0;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                ReadOnlySpan<byte> path = paths[pathIndex];
                CheckCancellation(ref examinedBytes, cancellationToken, checkpoint);
                if (path.Length == 0)
                {
                    if (paths.Count != 1)
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    continue;
                }

                if (pathIndex > 0
                    && CompareForOverlap(
                        path,
                        paths[pathIndex - 1],
                        ref examinedBytes,
                        cancellationToken,
                        checkpoint) == 0)
                {
                    throw new PayloadProtectionFormatException();
                }

                byte[] prefix = new byte[checked(path.Length + 1)];
                path.CopyTo(prefix);
                prefix[^1] = (byte)'/';
                descendantPrefixes[prefixCount++] = prefix;
            }

            int comparisons = 0;
            try
            {
                Array.Sort(descendantPrefixes, 0, prefixCount, Comparer<byte[]>.Create((left, right) =>
                {
                    CheckCancellation(ref comparisons, cancellationToken, checkpoint);
                    return Compare(left, right, cancellationToken, checkpoint);
                }));
            }
            catch (InvalidOperationException exception)
                when (cancellationToken.IsCancellationRequested
                    && exception.InnerException is OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            int intervalIndex = 0;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                ReadOnlySpan<byte> path = paths[pathIndex];
                while (intervalIndex < prefixCount
                    && CompareUpperBound(
                        descendantPrefixes[intervalIndex],
                        path,
                        ref examinedBytes,
                        cancellationToken,
                        checkpoint) <= 0)
                {
                    intervalIndex++;
                }

                if (intervalIndex < prefixCount
                    && CompareForOverlap(
                        descendantPrefixes[intervalIndex],
                        path,
                        ref examinedBytes,
                        cancellationToken,
                        checkpoint) <= 0)
                {
                    throw new PayloadProtectionFormatException();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            for (int index = 0; index < descendantPrefixes.Length; index++)
            {
                if (descendantPrefixes[index] is not null)
                {
                    CryptographicOperations.ZeroMemory(descendantPrefixes[index]);
                }
            }
        }
    }

    private static int CompareUpperBound(
        ReadOnlySpan<byte> lowerBound,
        ReadOnlySpan<byte> value,
        ref int examinedBytes,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        int common = Math.Min(lowerBound.Length, value.Length);
        for (int index = 0; index < common; index++)
        {
            CheckCancellation(ref examinedBytes, cancellationToken, checkpoint);
            byte lowerValue = index == lowerBound.Length - 1
                ? (byte)'0'
                : lowerBound[index];
            int comparison = lowerValue.CompareTo(value[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return lowerBound.Length.CompareTo(value.Length);
    }

    private static int CompareForOverlap(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        ref int examinedBytes,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        int common = Math.Min(left.Length, right.Length);
        for (int index = 0; index < common; index++)
        {
            CheckCancellation(ref examinedBytes, cancellationToken, checkpoint);
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static void CheckCancellation(
        ref int examined,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        examined = checked(examined + 1);
        if (examined == 1 || (examined & 255) == 0)
        {
            checkpoint?.Invoke(examined);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void CopyWithCancellation(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        int offset = 0;
        checkpoint?.Invoke(1);
        cancellationToken.ThrowIfCancellationRequested();
        while (offset < source.Length)
        {
            int length = Math.Min(256, source.Length - offset);
            source.Slice(offset, length).CopyTo(destination.Slice(offset, length));
            offset += length;
            checkpoint?.Invoke(offset);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
