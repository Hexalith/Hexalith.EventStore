using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Constructs the canonical protected-path manifest from normative section 7.2.
/// </summary>
internal static class ProtectedPathManifestCodec {
    /// <summary>
    /// Validates, sorts, and encodes a complete path set.
    /// </summary>
    internal static ProtectedPathManifest Create(IEnumerable<string> paths, bool snapshot = false) {
        ArgumentNullException.ThrowIfNull(paths);
        string[] values = paths.ToArray();
        if (values.Length is < 1 or > PayloadProtectionLimits.ProtectedPaths) {
            throw new PayloadProtectionFormatException();
        }

        var encodedPaths = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string path in values) {
            _ = JsonPointer.Decode(path, allowRoot: snapshot);
            if (snapshot != (path.Length == 0)
                || !encodedPaths.TryAdd(path, CanonicalText.Encode(path, snapshot ? 0 : 1, 2048))) {
                throw new PayloadProtectionFormatException();
            }
        }

        if (snapshot && (values.Length != 1 || values[0].Length != 0)) {
            throw new PayloadProtectionFormatException();
        }

        Array.Sort(values, (left, right) => Compare(encodedPaths[left], encodedPaths[right]));
        for (int index = 0; index < values.Length; index++) {
            for (int candidate = index + 1; candidate < values.Length; candidate++) {
                if (values[candidate].StartsWith(values[index] + "/", StringComparison.Ordinal)) {
                    throw new PayloadProtectionFormatException();
                }
            }
        }

        long totalLength = 9;
        foreach (string path in values) {
            totalLength = checked(totalLength + 4 + encodedPaths[path].Length);
        }

        if (totalLength > PayloadProtectionLimits.ManifestBytes) {
            throw new PayloadProtectionFormatException();
        }

        byte[] encoded = new byte[checked((int)totalLength)];
        "HXPM"u8.CopyTo(encoded);
        encoded[4] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(5), checked((uint)values.Length));
        int offset = 9;
        foreach (string path in values) {
            byte[] pathBytes = encodedPaths[path];
            BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(offset), checked((uint)pathBytes.Length));
            offset += 4;
            pathBytes.CopyTo(encoded, offset);
            offset += pathBytes.Length;
        }

        return new ProtectedPathManifest(values, encoded, SHA256.HashData(encoded));
    }

    private static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) {
        int common = Math.Min(left.Length, right.Length);
        for (int index = 0; index < common; index++) {
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0) {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
