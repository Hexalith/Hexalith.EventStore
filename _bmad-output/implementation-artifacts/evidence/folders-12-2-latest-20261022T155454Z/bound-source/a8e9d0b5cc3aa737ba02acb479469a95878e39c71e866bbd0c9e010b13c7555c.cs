using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.DomainService;

/// <summary>Derives an opaque private session key without exposing tenant or projection identity.</summary>
internal static class DomainSharedProjectionRebuildSessionKey {
    /// <summary>Computes the v1 private session key.</summary>
    public static string Compute(DomainSharedProjectionRebuildIdentity identity) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, identity.TenantId);
        Append(hash, identity.Domain);
        Append(hash, identity.ProjectionType);
        Append(hash, identity.OperationId);
        return "readmodel:internal:shared-rebuild:v1:"
            + Convert.ToBase64String(hash.GetHashAndReset()).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static void Append(IncrementalHash hash, string value) {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
