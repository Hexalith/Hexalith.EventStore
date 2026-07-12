using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Computes the versioned canonical fingerprint that binds a batch identity to its exact operations.
/// </summary>
/// <remarks>
/// The fingerprint material includes, for every operation in ordinal order: the ordinal, logical key,
/// write/delete kind, stable value type identity, concurrency mode and expected ETag, and the canonical
/// DAPR-compatible JSON value bytes. The scope components participate as well. The algorithm is frozen at
/// v1 and covered by golden-vector tests; changing it is a versioned contract change.
/// </remarks>
internal static class ReadModelBatchFingerprint {
    /// <summary>The frozen fingerprint algorithm version.</summary>
    public const int Version = 1;

    private static readonly JsonWriterOptions s_writerOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Builds the deterministic canonical manifest bytes for a batch.</summary>
    /// <param name="batch">The batch.</param>
    /// <returns>The canonical manifest UTF-8 bytes.</returns>
    public static byte[] BuildCanonicalManifest(ReadModelBatch batch) {
        ArgumentNullException.ThrowIfNull(batch);
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer, s_writerOptions)) {
            writer.WriteStartObject();
            writer.WriteNumber("v", Version);

            writer.WritePropertyName("scope");
            writer.WriteStartObject();
            writer.WriteString("store", batch.Scope.StoreName);
            writer.WriteString("tenant", batch.Scope.TenantId);
            writer.WriteString("domain", batch.Scope.Domain);
            writer.WriteString("aggregate", batch.Scope.AggregateId);
            writer.WriteString("projection", batch.Scope.ProjectionType);
            writer.WriteString("batch", batch.Scope.BatchId);
            writer.WriteEndObject();

            writer.WritePropertyName("ops");
            writer.WriteStartArray();
            for (int ordinal = 0; ordinal < batch.Operations.Count; ordinal++) {
                ReadModelBatchOperation operation = batch.Operations[ordinal];
                writer.WriteStartObject();
                writer.WriteNumber("ord", ordinal);
                writer.WriteString("key", operation.Key);
                writer.WriteString("kind", operation.Kind == ReadModelBatchOperationKind.Write ? "w" : "d");
                writer.WriteString("type", operation.ValueTypeName);
                writer.WriteString("cmode", operation.Concurrency.Mode.ToString());
                writer.WriteString("etag", operation.Concurrency.ExpectedETag);
                writer.WriteBase64String("val", operation.CanonicalValue.Span);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Computes the versioned fingerprint string for a batch.</summary>
    /// <param name="batch">The batch.</param>
    /// <returns>A <c>v1:</c>-prefixed base64url SHA-256 digest of the canonical manifest.</returns>
    public static string Compute(ReadModelBatch batch) => ComputeFromManifest(BuildCanonicalManifest(batch));

    /// <summary>Computes the versioned fingerprint string from already-built canonical manifest bytes.</summary>
    /// <param name="manifest">The canonical manifest bytes.</param>
    /// <returns>A <c>v1:</c>-prefixed base64url SHA-256 digest.</returns>
    public static string ComputeFromManifest(ReadOnlySpan<byte> manifest) {
        byte[] digest = SHA256.HashData(manifest);
        return "v" + Version + ":" + Base64Url(digest);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
