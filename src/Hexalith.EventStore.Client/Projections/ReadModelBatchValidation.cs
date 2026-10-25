namespace Hexalith.EventStore.Client.Projections;

/// <summary>Validates the complete immutable batch before any storage operation.</summary>
public static class ReadModelBatchValidation
{
    /// <summary>Checks deployment and recovery limits without accessing storage.</summary>
    /// <param name="batch">The complete batch.</param>
    /// <param name="options">The owning store limits.</param>
    public static void Validate(ReadModelBatch batch, ReadModelBatchOptions options) {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (batch.RecoveryRequestDigest is not null && (!batch.DiscoverableRecovery
            || batch.RecoveryRequestDigest.Length != 64 || batch.RecoveryRequestDigest.Any(static ch => !char.IsAsciiHexDigit(ch))))
            throw new ArgumentException("Invalid private recovery request digest.", nameof(batch));
        if (batch.DiscoverableRecovery && !batch.RetainManifestForRecovery && batch.OwnerSelection is null && batch.OwnerSelections is null)
            throw new ArgumentException("Discoverable preparation requires a retained recovery manifest.", nameof(batch));
        if (batch.Operations.Count > options.MaxOperations
            || batch.RetainManifestForRecovery && batch.Operations.Count > ReadModelBatchOptions.DefaultMaxOperations) {
            throw new ArgumentException(
                $"Read-model batch has {batch.Operations.Count} operations, exceeding the configured maximum of {options.MaxOperations}.",
                nameof(batch));
        }

        foreach (ReadModelBatchOperation operation in batch.Operations) {
            int keyBytes = System.Text.Encoding.UTF8.GetByteCount(operation.Key);
            if (keyBytes > options.MaxKeyByteLength
                || batch.RetainManifestForRecovery && keyBytes > ReadModelBatchOptions.DefaultMaxKeyByteLength) {
                throw new ArgumentException(
                    $"Read-model batch logical key exceeds the configured maximum of {options.MaxKeyByteLength} UTF-8 bytes.",
                    nameof(batch));
            }

            // Guard against a concurrency policy built through the raw record constructor with a null
            // expected ETag (the factory methods guard, but the public constructor cannot). Fail before
            // any state access rather than dereferencing a null ETag mid-install.
            if (operation.Concurrency.ExpectedETag is null) {
                throw new ArgumentException(
                    "Read-model batch operation has a null expected ETag; use the ReadModelBatchConcurrency factory methods.",
                    nameof(batch));
            }
        }

        byte[] manifest = ReadModelBatchFingerprint.BuildCanonicalManifest(batch);
        if (manifest.Length > options.MaxCanonicalManifestBytes
            || batch.RetainManifestForRecovery && manifest.Length > ReadModelBatchOptions.DefaultMaxCanonicalManifestBytes) {
            throw new ArgumentException(
                $"Read-model batch canonical manifest is {manifest.Length} bytes, exceeding the configured maximum of {options.MaxCanonicalManifestBytes}.",
                nameof(batch));
        }
    }

}
