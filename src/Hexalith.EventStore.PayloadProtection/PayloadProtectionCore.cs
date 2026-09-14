using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements the provider-neutral pdenc-v2 JSON/AAD/AES core for normative sections 5-8 and 14-15.
/// </summary>
internal sealed class PayloadProtectionCore(ISensitiveBufferObserver? observer = null) {
    /// <summary>
    /// Protects the selected event paths as one atomic transformation. Material is requested only after
    /// local validation finds at least one non-null value; ownership of the returned DEK transfers to this call.
    /// </summary>
    internal CoreProtectionResult ProtectEvent(
        byte[] payloadBytes,
        IReadOnlyCollection<string> selectedPaths,
        PayloadProtectionContext context,
        Func<PayloadProtectionMaterial> materialFactory,
        int maximumProtectedValueBytes = PayloadProtectionLimits.CiphertextBytes,
        CancellationToken cancellationToken = default,
        Action<int>? encryptionCheckpoint = null) {
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentNullException.ThrowIfNull(selectedPaths);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(materialFactory);
        Stopwatch stopwatch = Stopwatch.StartNew();
        using Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Protect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        PayloadProtectionMaterial? material = null;
        Dictionary<string, byte[]>? rawValues = null;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.PayloadKind != PayloadProtectionPayloadKind.Event
                || maximumProtectedValueBytes is < 1 or > PayloadProtectionLimits.CiphertextBytes) {
                throw new PayloadProtectionFormatException();
            }

            using BoundedJsonDocument document = BoundedJsonDocument.Parse(payloadBytes, cancellationToken);
            if (document.ContainsProtectedMember) {
                throw new PayloadProtectionFormatException();
            }

            if (selectedPaths.Count == 0) {
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return new CoreProtectionResult(payloadBytes, "json", 0);
            }

            _ = ProtectedPathManifestCodec.Create(selectedPaths);
            var nonNullPaths = new List<string>(selectedPaths.Count);
            rawValues = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long totalPlaintext = 0;
            foreach (string path in selectedPaths) {
                cancellationToken.ThrowIfCancellationRequested();
                JsonElement value = JsonPointer.Resolve(document.Document.RootElement, path);
                if (value.ValueKind == JsonValueKind.Null) {
                    continue;
                }

                byte[] raw = Encoding.UTF8.GetBytes(value.GetRawText());
                totalPlaintext = checked(totalPlaintext + raw.Length);
                if (raw.Length > maximumProtectedValueBytes
                    || totalPlaintext > PayloadProtectionLimits.SelectedPlaintextBytes) {
                    Clear(raw, SensitiveBufferKind.SelectedPlaintext);
                    throw new PayloadProtectionFormatException();
                }

                nonNullPaths.Add(path);
                rawValues.Add(path, raw);
            }

            if (nonNullPaths.Count == 0) {
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return new CoreProtectionResult(payloadBytes, "json", 0);
            }

            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(nonNullPaths);
            material = materialFactory();
            if (material is null
                || !CanonicalUlid.IsValid(material.KeyReference)
                || material.DekVersion == 0
                || material.DataEncryptionKey.Length != 32) {
                throw new PayloadProtectionFormatException();
            }

            for (int index = 0; index < manifest.Paths.Count; index++) {
                encryptionCheckpoint?.Invoke(index);
                cancellationToken.ThrowIfCancellationRequested();
                string path = manifest.Paths[index];
                byte[] raw = rawValues[path];
                byte[] aad = AadCodec.Write(
                    context,
                    path,
                    material.KeyReference,
                    material.DekVersion,
                    checked((uint)index),
                    manifest.Commitment);
                PayloadProtectionEnvelope envelope = PayloadCryptography.Encrypt(
                    raw,
                    aad,
                    material.DataEncryptionKey,
                    material.KeyReference,
                    material.DekVersion,
                    checked((uint)index));
                string encoded = Base64UrlCodec.Encode(EnvelopeCodec.Write(envelope));
                JsonPointer.Replace(document.Root, path, new JsonObject { ["$pdenc"] = encoded });
            }

            byte[] transformed = Serialize(document.Root);
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return new CoreProtectionResult(transformed, "json+pdenc-v2", manifest.Paths.Count);
        }
        catch (OperationCanceledException) {
            diagnosticResult = PayloadProtectionDiagnosticResult.Cancelled;
            throw;
        }
        finally {
            if (rawValues is not null) {
                foreach (byte[] raw in rawValues.Values) {
                    Clear(raw, SensitiveBufferKind.SelectedPlaintext);
                }
            }

            if (material is not null) {
                Clear(material.DataEncryptionKey, SensitiveBufferKind.DataEncryptionKey);
            }
            PayloadProtectionDiagnostics.Record(PayloadProtectionOperation.Protect, diagnosticResult, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Authenticates every wrapper before returning any plaintext. A resolver result transfers key ownership to this call.
    /// </summary>
    internal async ValueTask<CoreUnprotectionResult> TryUnprotectEventAsync(
        byte[] protectedPayloadBytes,
        PayloadProtectionContext context,
        Func<string, uint, CancellationToken, ValueTask<byte[]?>> keyResolver,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(protectedPayloadBytes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keyResolver);
        Stopwatch stopwatch = Stopwatch.StartNew();
        using Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Unprotect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        byte[]? dek = null;
        try {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.PayloadKind != PayloadProtectionPayloadKind.Event) {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            using BoundedJsonDocument document = BoundedJsonDocument.Parse(protectedPayloadBytes, cancellationToken);
            var wrappers = new List<ProtectedWrapper>();
            EnumerateWrappers(document.Document.RootElement, string.Empty, wrappers, cancellationToken);
            if (wrappers.Count is < 1 or > PayloadProtectionLimits.ProtectedPaths) {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(wrappers.Select(static wrapper => wrapper.Path));
            Dictionary<string, ProtectedWrapper> wrappersByPath = wrappers.ToDictionary(static item => item.Path, StringComparer.Ordinal);
            ProtectedWrapper first = wrappersByPath[manifest.Paths[0]];
            for (int index = 0; index < manifest.Paths.Count; index++) {
                ProtectedWrapper wrapper = wrappersByPath[manifest.Paths[index]];
                if (wrapper.Envelope.FieldOrdinal != index
                    || !string.Equals(wrapper.Envelope.KeyReference, first.Envelope.KeyReference, StringComparison.Ordinal)
                    || wrapper.Envelope.DekVersion != first.Envelope.DekVersion) {
                    return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            try {
                dek = await keyResolver(
                    first.Envelope.KeyReference,
                    first.Envelope.DekVersion,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch {
                diagnosticResult = PayloadProtectionDiagnosticResult.Unavailable;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (dek is null) {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.MissingKey);
            }

            if (dek.Length != 32) {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ConsistencyMismatch);
            }

            foreach (string path in manifest.Paths) {
                cancellationToken.ThrowIfCancellationRequested();
                ProtectedWrapper wrapper = wrappersByPath[path];
                byte[] aad = AadCodec.Write(
                    context,
                    path,
                    wrapper.Envelope.KeyReference,
                    wrapper.Envelope.DekVersion,
                    wrapper.Envelope.FieldOrdinal,
                    manifest.Commitment);
                byte[] plaintext = PayloadCryptography.Decrypt(wrapper.Envelope, aad, dek, observer);
                try {
                    if (plaintext.Length > PayloadProtectionLimits.CiphertextBytes) {
                        throw new PayloadProtectionFormatException();
                    }

                    using BoundedJsonDocument plaintextDocument = BoundedJsonDocument.Parse(plaintext, cancellationToken);
                    JsonPointer.Replace(document.Root, path, plaintextDocument.Root.DeepClone());
                }
                finally {
                    Clear(plaintext, SensitiveBufferKind.DecryptedPlaintext);
                }
            }

            byte[] result = Serialize(document.Root);
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return CoreUnprotectionResult.Readable(result);
        }
        catch (OperationCanceledException) {
            diagnosticResult = PayloadProtectionDiagnosticResult.Cancelled;
            throw;
        }
        catch (PayloadProtectionAuthenticationException) {
            diagnosticResult = PayloadProtectionDiagnosticResult.AuthenticationFailed;
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
        catch (PayloadProtectionFormatException) {
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
        finally {
            if (dek is not null) {
                Clear(dek, SensitiveBufferKind.DataEncryptionKey);
            }

            PayloadProtectionDiagnostics.Record(PayloadProtectionOperation.Unprotect, diagnosticResult, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static void EnumerateWrappers(
        JsonElement element,
        string path,
        ICollection<ProtectedWrapper> wrappers,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        switch (element.ValueKind) {
            case JsonValueKind.Object:
                if (element.TryGetProperty("$pdenc", out JsonElement protectedValue)) {
                    if (path.Length == 0
                        || element.EnumerateObject().Count() != 1
                        || protectedValue.ValueKind != JsonValueKind.String) {
                        throw new PayloadProtectionFormatException();
                    }

                    byte[] envelopeBytes = Base64UrlCodec.Decode(protectedValue.GetString());
                    wrappers.Add(new ProtectedWrapper(path, EnvelopeCodec.Read(envelopeBytes)));
                    return;
                }

                foreach (JsonProperty property in element.EnumerateObject()) {
                    EnumerateWrappers(
                        property.Value,
                        path + "/" + JsonPointer.Escape(property.Name),
                        wrappers,
                        cancellationToken);
                }

                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray()) {
                    EnumerateWrappers(item, path + "/" + index, wrappers, cancellationToken);
                    index++;
                }

                break;
        }
    }

    private void Clear(byte[] buffer, SensitiveBufferKind kind) {
        CryptographicOperations.ZeroMemory(buffer);
        observer?.BufferCleared(kind, buffer);
    }

    private static byte[] Serialize(JsonNode root) {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output)) {
            root.WriteTo(writer);
        }

        return output.WrittenSpan.ToArray();
    }
}
