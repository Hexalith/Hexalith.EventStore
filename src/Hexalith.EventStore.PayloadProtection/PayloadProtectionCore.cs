// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 5-8, 14, and 15.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Implements the provider-neutral pdenc-v2 byte-oriented JSON/AAD/AES core for normative sections 5-8 and 14-15.
/// </summary>
internal sealed class PayloadProtectionCore(ISensitiveBufferObserver? observer = null)
{
    private static ReadOnlySpan<byte> WrapperPrefix => "{\"$pdenc\":\""u8;
    private static ReadOnlySpan<byte> WrapperSuffix => "\"}"u8;

    /// <summary>
    /// Protects selected event paths as one atomic transformation using stable byte and path snapshots.
    /// </summary>
    /// <param name="payloadBytes">The caller-owned serialized event JSON. It is snapshotted and never mutated.</param>
    /// <param name="selectedPaths">The canonical RFC 6901 paths to protect.</param>
    /// <param name="context">The trusted authenticated event occurrence.</param>
    /// <param name="materialFactory">
    /// Supplies the cryptographic material for this one payload. The factory MUST return a wholly fresh DEK and key
    /// reference on every invocation: normative section 8.2 derives each nonce from the field ordinal alone, so reusing
    /// a DEK across two payloads repeats an AES-GCM (key, nonce) pair and destroys confidentiality and integrity for
    /// both. The core cannot detect that reuse. Ownership of the returned mutable DEK transfers to the core, which
    /// clears it on every exit.
    /// </param>
    /// <param name="maximumProtectedValueBytes">The configured per-value write ceiling.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <param name="encryptionCheckpoint">An optional per-ordinal encryption test checkpoint.</param>
    /// <param name="traversalCheckpoint">An optional bounded-work test checkpoint for parsing, manifest and rewrite phases.</param>
    /// <returns>The complete transformed payload, or the unchanged payload when nothing was selected.</returns>
    internal CoreProtectionResult ProtectEvent(
        byte[] payloadBytes,
        IReadOnlyCollection<string> selectedPaths,
        PayloadProtectionContext context,
        Func<PayloadProtectionMaterial> materialFactory,
        int maximumProtectedValueBytes = PayloadProtectionLimits.CiphertextBytes,
        CancellationToken cancellationToken = default,
        Action<int>? encryptionCheckpoint = null,
        Action<int>? traversalCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentNullException.ThrowIfNull(selectedPaths);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(materialFactory);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Protect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        PayloadProtectionMaterial? material = null;
        ProtectedPathManifest? requestedManifest = null;
        ProtectedPathManifest? manifest = null;
        List<(BoundedJsonNode Node, byte[] Plaintext)>? selectedValues = null;
        List<JsonReplacement>? replacements = null;
        byte[]? transformed = null;
        bool protectedDiagnosticFormat = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maximumProtectedValueBytes is < 1 or > PayloadProtectionLimits.CiphertextBytes)
            {
                throw new PayloadProtectionFormatException();
            }

            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Event);
            requestedManifest = ProtectedPathManifestCodec.Create(
                selectedPaths,
                cancellationToken: cancellationToken,
                allowEmpty: true,
                checkpoint: traversalCheckpoint);
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(
                payloadBytes,
                cancellationToken,
                traversalCheckpoint,
                observer: observer,
                ownedBufferKind: SensitiveBufferKind.InputSnapshot);
            if (document.ContainsProtectedMember)
            {
                throw new PayloadProtectionFormatException();
            }

            if (requestedManifest.Paths.Count == 0)
            {
                transformed = document.CopyPayload(cancellationToken);
                var result = new CoreProtectionResult(
                    transformed,
                    PayloadProtectionWireFormat.UnprotectedSerializationFormat,
                    0);
                transformed = null;
                protectedDiagnosticFormat = false;
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return result;
            }

            selectedValues = new List<(BoundedJsonNode Node, byte[] Plaintext)>(requestedManifest.Paths.Count);
            var nonNullPaths = new List<string>(requestedManifest.Paths.Count);
            long totalPlaintext = 0;
            long prospectiveOutputBytes = payloadBytes.Length;
            int prospectiveNodeCount = document.NodeCount;
            for (int index = 0; index < requestedManifest.Paths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = requestedManifest.Paths[index];
                BoundedJsonNode node = document.Resolve(
                    path,
                    cancellationToken: cancellationToken,
                    checkpoint: traversalCheckpoint);
                if (node.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (node.Length > maximumProtectedValueBytes)
                {
                    throw new PayloadProtectionFormatException();
                }

                totalPlaintext = checked(totalPlaintext + node.Length);
                if (totalPlaintext > PayloadProtectionLimits.SelectedPlaintextBytes)
                {
                    throw new PayloadProtectionFormatException();
                }

                prospectiveOutputBytes = checked(
                    prospectiveOutputBytes - node.Length + GetWrapperLength(node.Length));
                prospectiveNodeCount = checked(
                    prospectiveNodeCount - BoundedJsonDocument.GetSubtreeNodeCount(node) + 2);
                if (prospectiveOutputBytes > PayloadProtectionLimits.PayloadBytes
                    || prospectiveNodeCount > PayloadProtectionLimits.JsonNodes
                    || BoundedJsonDocument.GetDepth(node) + 1 > PayloadProtectionLimits.JsonDepth)
                {
                    throw new PayloadProtectionFormatException();
                }

                byte[] plaintext = document.CopyRawValue(node);
                selectedValues.Add((node, plaintext));
                nonNullPaths.Add(path);
            }

            if (nonNullPaths.Count == 0)
            {
                transformed = document.CopyPayload(cancellationToken);
                var result = new CoreProtectionResult(
                    transformed,
                    PayloadProtectionWireFormat.UnprotectedSerializationFormat,
                    0);
                transformed = null;
                protectedDiagnosticFormat = false;
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return result;
            }

            manifest = ProtectedPathManifestCodec.Create(
                nonNullPaths,
                cancellationToken: cancellationToken,
                checkpoint: traversalCheckpoint);
            if (selectedValues.Count != manifest.Paths.Count)
            {
                throw new PayloadProtectionFormatException();
            }

            for (int index = 0; index < manifest.Paths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // requestedManifest.Paths is canonical and filtering nulls preserves its order. Assert that
                // recreating the non-null manifest cannot detach a selected plaintext from its ordinal.
                if (!string.Equals(nonNullPaths[index], manifest.Paths[index], StringComparison.Ordinal))
                {
                    throw new PayloadProtectionFormatException();
                }

                _ = AadCodec.ValidateBeforeMaterial(
                    context,
                    manifest.Paths[index],
                    checked((uint)index),
                    manifest.Commitment,
                    cancellationToken,
                    traversalCheckpoint);
            }

            cancellationToken.ThrowIfCancellationRequested();
            PayloadCryptography.EnsurePlatformSupport();
            material = InvokeMaterialFactory(materialFactory, cancellationToken);
            material = ValidateMaterial(material);
            replacements = new List<JsonReplacement>(manifest.Paths.Count);
            for (int index = 0; index < manifest.Paths.Count; index++)
            {
                encryptionCheckpoint?.Invoke(index);
                cancellationToken.ThrowIfCancellationRequested();
                string path = manifest.Paths[index];
                (BoundedJsonNode node, byte[] plaintext) = selectedValues[index];
                byte[] aad = AadCodec.Write(
                    context,
                    path,
                    material.KeyReference,
                    material.DekVersion,
                    checked((uint)index),
                    manifest.Commitment,
                    cancellationToken);
                PayloadProtectionEnvelope? envelope = null;
                byte[]? envelopeBytes = null;
                try
                {
                    envelope = PayloadCryptography.Encrypt(
                        plaintext,
                        aad,
                        material.DataEncryptionKey,
                        material.KeyReference,
                        material.DekVersion,
                        checked((uint)index));
                    envelopeBytes = EnvelopeCodec.Write(envelope);
                    string encodedEnvelope = Base64UrlCodec.Encode(envelopeBytes);
                    byte[]? wrapper = CreateWrapper(encodedEnvelope);
                    try
                    {
                        var replacement = new JsonReplacement(node.Start, node.Length, wrapper);
                        replacements.Add(replacement);
                        wrapper = null;
                    }
                    finally
                    {
                        if (wrapper is not null)
                        {
                            Clear(wrapper, SensitiveBufferKind.ProtectedOutput);
                        }
                    }
                }
                finally
                {
                    Clear(aad, SensitiveBufferKind.AuthenticatedData);
                    if (envelopeBytes is not null)
                    {
                        Clear(envelopeBytes, SensitiveBufferKind.ProtectedOutput);
                    }

                    if (envelope is not null)
                    {
                        ClearEnvelope(envelope);
                    }
                }
            }

            transformed = document.Rewrite(replacements, cancellationToken, traversalCheckpoint);
            using (BoundedJsonDocument validated = BoundedJsonDocument.Inspect(transformed, cancellationToken))
            {
                _ = validated.NodeCount;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var protectionResult = new CoreProtectionResult(
                transformed,
                PayloadProtectionWireFormat.ProtectedSerializationFormat,
                manifest.Paths.Count);
            transformed = null;
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return protectionResult;
        }
        catch (OperationCanceledException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.Cancelled;
            throw;
        }
        catch (PayloadProtectionCryptographicException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.CryptographicFailure;
            throw;
        }
        catch (OverflowException)
        {
            throw new PayloadProtectionFormatException();
        }
        finally
        {
            if (transformed is not null)
            {
                Clear(transformed, SensitiveBufferKind.AbandonedOutput);
            }

            if (replacements is not null)
            {
                for (int index = 0; index < replacements.Count; index++)
                {
                    Clear(replacements[index].Value, SensitiveBufferKind.ProtectedOutput);
                }
            }

            if (selectedValues is not null)
            {
                for (int index = 0; index < selectedValues.Count; index++)
                {
                    Clear(selectedValues[index].Plaintext, SensitiveBufferKind.SelectedPlaintext);
                }
            }

            if (material?.DataEncryptionKey is not null)
            {
                Clear(material.DataEncryptionKey, SensitiveBufferKind.DataEncryptionKey);
            }

            ClearManifest(manifest);
            ClearManifest(requestedManifest);

            PayloadProtectionDiagnostics.Stop(activity, diagnosticResult);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Protect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds,
                protectedDiagnosticFormat);
        }
    }

    /// <summary>
    /// Protects one complete snapshot as the root-path v2 envelope without Server or registry integration.
    /// </summary>
    /// <param name="snapshotBytes">The caller-owned serialized snapshot JSON. It is snapshotted and never mutated.</param>
    /// <param name="context">The trusted authenticated snapshot occurrence.</param>
    /// <param name="materialFactory">
    /// Supplies the cryptographic material for this one snapshot under the same contract as
    /// <see cref="ProtectEvent"/>: a wholly fresh DEK and key reference per invocation, because the ordinal-derived
    /// nonce of normative section 8.2 makes any DEK reuse an AES-GCM (key, nonce) repeat the core cannot detect.
    /// Ownership of the returned mutable DEK transfers to the core, which clears it on every exit.
    /// </param>
    /// <param name="maximumProtectedValueBytes">The configured snapshot write ceiling.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The durable protected snapshot carrier.</returns>
    internal ProtectedSnapshotPayloadV2 ProtectSnapshot(
        byte[] snapshotBytes,
        PayloadProtectionContext context,
        Func<PayloadProtectionMaterial> materialFactory,
        int maximumProtectedValueBytes = PayloadProtectionLimits.CiphertextBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshotBytes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(materialFactory);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Protect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        PayloadProtectionMaterial? material = null;
        ProtectedPathManifest? manifest = null;
        byte[]? plaintext = null;
        byte[]? envelopeBytes = null;
        PayloadProtectionEnvelope? envelope = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maximumProtectedValueBytes is < 1 or > PayloadProtectionLimits.CiphertextBytes
                || snapshotBytes.Length > maximumProtectedValueBytes)
            {
                throw new PayloadProtectionFormatException();
            }

            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Snapshot);
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(
                snapshotBytes,
                cancellationToken,
                observer: observer,
                ownedBufferKind: SensitiveBufferKind.InputSnapshot);
            if (document.ContainsProtectedMember)
            {
                throw new PayloadProtectionFormatException();
            }

            plaintext = document.CopyPayload(cancellationToken);
            manifest = ProtectedPathManifestCodec.Create(
                [string.Empty],
                snapshot: true,
                cancellationToken: cancellationToken);
            _ = AadCodec.ValidateBeforeMaterial(
                context,
                string.Empty,
                0,
                manifest.Commitment,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PayloadCryptography.EnsurePlatformSupport();
            material = InvokeMaterialFactory(materialFactory, cancellationToken);
            material = ValidateMaterial(material);
            byte[] aad = AadCodec.Write(
                context,
                string.Empty,
                material.KeyReference,
                material.DekVersion,
                0,
                manifest.Commitment,
                cancellationToken);
            try
            {
                envelope = PayloadCryptography.Encrypt(
                    plaintext,
                    aad,
                    material.DataEncryptionKey,
                    material.KeyReference,
                    material.DekVersion,
                    0);
                envelopeBytes = EnvelopeCodec.Write(envelope);
                string encodedEnvelope = Base64UrlCodec.Encode(envelopeBytes);
                cancellationToken.ThrowIfCancellationRequested();
                var result = new ProtectedSnapshotPayloadV2(
                    PayloadProtectionWireFormat.ProtectedSerializationFormat,
                    context.PayloadTypeId,
                    encodedEnvelope);
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return result;
            }
            finally
            {
                Clear(aad, SensitiveBufferKind.AuthenticatedData);
            }
        }
        catch (OperationCanceledException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.Cancelled;
            throw;
        }
        catch (PayloadProtectionCryptographicException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.CryptographicFailure;
            throw;
        }
        catch (OverflowException)
        {
            throw new PayloadProtectionFormatException();
        }
        finally
        {
            if (envelopeBytes is not null)
            {
                Clear(envelopeBytes, SensitiveBufferKind.ProtectedOutput);
            }

            if (envelope is not null)
            {
                ClearEnvelope(envelope);
            }

            if (plaintext is not null)
            {
                Clear(plaintext, SensitiveBufferKind.SelectedPlaintext);
            }

            if (material?.DataEncryptionKey is not null)
            {
                Clear(material.DataEncryptionKey, SensitiveBufferKind.DataEncryptionKey);
            }

            ClearManifest(manifest);

            PayloadProtectionDiagnostics.Stop(activity, diagnosticResult);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Protect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Authenticates every event wrapper before transferring any reconstructed plaintext.
    /// </summary>
    /// <param name="protectedPayloadBytes">The caller-owned protected JSON bytes.</param>
    /// <param name="context">The trusted authenticated event occurrence.</param>
    /// <param name="keyResolver">
    /// Resolves an exact key reference and version. Ownership of every returned non-null mutable DEK transfers to the core,
    /// which clears it on every exit.
    /// </param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <param name="traversalCheckpoint">An optional bounded-work test checkpoint for every traversal counter.</param>
    /// <param name="rewriteSortCheckpoint">
    /// An optional checkpoint for the rewrite replacement-sort counter. It is separate from
    /// <paramref name="traversalCheckpoint"/> because the two count independent work and binding one delegate to both
    /// interleaves them into a single non-monotonic stream that no bounded-work assertion can read.
    /// </param>
    /// <returns>The complete authenticated event JSON or one bounded unreadable reason.</returns>
    internal async ValueTask<CoreUnprotectionResult> TryUnprotectEventAsync(
        byte[] protectedPayloadBytes,
        PayloadProtectionContext context,
        Func<string, uint, CancellationToken, ValueTask<byte[]?>> keyResolver,
        CancellationToken cancellationToken = default,
        Action<int>? traversalCheckpoint = null,
        Action<int>? rewriteSortCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(protectedPayloadBytes);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keyResolver);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Unprotect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        byte[]? dek = null;
        byte[]? reconstructed = null;
        IReadOnlyList<ProtectedWrapper>? wrappers = null;
        ProtectedPathManifest? manifest = null;
        List<byte[]>? plaintextBuffers = null;
        int clearedPlaintextBuffers = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Event);
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(
                protectedPayloadBytes,
                cancellationToken,
                traversalCheckpoint,
                observer: observer,
                ownedBufferKind: SensitiveBufferKind.InputSnapshot);
            wrappers = document.ReadProtectedWrappers(cancellationToken, traversalCheckpoint);
            if (wrappers.Count < 1)
            {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            string[] wrapperPaths = new string[wrappers.Count];
            for (int index = 0; index < wrappers.Count; index++)
            {
                CheckCancellation(index, cancellationToken, traversalCheckpoint);
                wrapperPaths[index] = wrappers[index].Path;
            }

            manifest = ProtectedPathManifestCodec.Create(
                wrapperPaths,
                cancellationToken: cancellationToken,
                checkpoint: traversalCheckpoint);
            ProtectedWrapper? first = null;
            var wrappersByPath = new Dictionary<string, ProtectedWrapper>(wrappers.Count, StringComparer.Ordinal);
            for (int index = 0; index < wrappers.Count; index++)
            {
                CheckCancellation(index, cancellationToken, traversalCheckpoint);
                if (!wrappersByPath.TryAdd(wrappers[index].Path, wrappers[index]))
                {
                    return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }
            }

            var orderedWrappers = new ProtectedWrapper[wrappers.Count];
            long totalCiphertext = 0;
            long prospectiveOutputBytes = protectedPayloadBytes.Length;
            for (int index = 0; index < manifest.Paths.Count; index++)
            {
                CheckCancellation(index, cancellationToken, traversalCheckpoint);
                if (!wrappersByPath.TryGetValue(manifest.Paths[index], out ProtectedWrapper? wrapper))
                {
                    return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }

                orderedWrappers[index] = wrapper;
                first ??= wrapper;
                if (wrapper.Envelope.FieldOrdinal != index
                    || !string.Equals(wrapper.Envelope.KeyReference, first.Envelope.KeyReference, StringComparison.Ordinal)
                    || wrapper.Envelope.DekVersion != first.Envelope.DekVersion)
                {
                    return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }

                _ = AadCodec.Validate(
                    context,
                    wrapper.Path,
                    wrapper.Envelope.KeyReference,
                    wrapper.Envelope.DekVersion,
                    wrapper.Envelope.FieldOrdinal,
                    manifest.Commitment,
                    cancellationToken,
                    traversalCheckpoint);
                CheckCancellation(index, cancellationToken, traversalCheckpoint);
                totalCiphertext = checked(totalCiphertext + wrapper.Envelope.Ciphertext.Length);
                prospectiveOutputBytes = checked(
                    prospectiveOutputBytes - wrapper.Length + wrapper.Envelope.Ciphertext.Length);
                if (totalCiphertext > PayloadProtectionLimits.SelectedPlaintextBytes
                    || prospectiveOutputBytes > PayloadProtectionLimits.PayloadBytes)
                {
                    return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }
            }

            plaintextBuffers = new List<byte[]>(orderedWrappers.Length);
            cancellationToken.ThrowIfCancellationRequested();
            PayloadCryptography.EnsurePlatformSupport();
            try
            {
                dek = await keyResolver(
                    first!.Envelope.KeyReference,
                    first.Envelope.DekVersion,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnosticResult = PayloadProtectionDiagnosticResult.Unavailable;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (dek is null)
            {
                diagnosticResult = PayloadProtectionDiagnosticResult.MissingKey;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.MissingKey);
            }

            if (dek.Length != 32)
            {
                diagnosticResult = PayloadProtectionDiagnosticResult.ConsistencyMismatch;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ConsistencyMismatch);
            }

            var replacements = new List<JsonReplacement>(orderedWrappers.Length);
            int prospectiveNodeCount = checked(document.NodeCount - (orderedWrappers.Length * 2));

            for (int index = 0; index < orderedWrappers.Length; index++)
            {
                CheckCancellation(index, cancellationToken, traversalCheckpoint);
                ProtectedWrapper wrapper = orderedWrappers[index];
                byte[] aad = AadCodec.Write(
                    context,
                    wrapper.Path,
                    wrapper.Envelope.KeyReference,
                    wrapper.Envelope.DekVersion,
                    wrapper.Envelope.FieldOrdinal,
                    manifest.Commitment,
                    cancellationToken,
                    traversalCheckpoint);
                byte[] plaintext;
                try
                {
                    plaintext = PayloadCryptography.Decrypt(wrapper.Envelope, aad, dek, observer);
                }
                finally
                {
                    Clear(aad, SensitiveBufferKind.AuthenticatedData);
                }

                plaintextBuffers.Add(plaintext);
                if (!PayloadCryptography.HasExpectedNonce(wrapper.Envelope))
                {
                    throw new PayloadProtectionFormatException();
                }

                int remainingNodes = checked(PayloadProtectionLimits.JsonNodes - prospectiveNodeCount);
                int wrapperDepth = BoundedJsonDocument.GetDepth(document.Resolve(
                    wrapper.Path,
                    cancellationToken: cancellationToken,
                    checkpoint: traversalCheckpoint));
                int remainingDepth = checked(PayloadProtectionLimits.JsonDepth - wrapperDepth);
                if (remainingNodes < 1 || remainingDepth < 1)
                {
                    throw new PayloadProtectionFormatException();
                }

                using (BoundedJsonDocument plaintextDocument = BoundedJsonDocument.Inspect(
                    plaintext,
                    cancellationToken,
                    maximumNodes: remainingNodes,
                    maximumDepth: remainingDepth))
                {
                    if (plaintextDocument.Root.ValueKind == JsonValueKind.Null
                        || plaintextDocument.ContainsProtectedMember
                        || wrapperDepth + plaintextDocument.MaximumDepth > PayloadProtectionLimits.JsonDepth)
                    {
                        throw new PayloadProtectionFormatException();
                    }

                    prospectiveNodeCount = checked(prospectiveNodeCount + plaintextDocument.NodeCount);
                }

                replacements.Add(new JsonReplacement(wrapper.Start, wrapper.Length, plaintext));
                cancellationToken.ThrowIfCancellationRequested();
            }

            reconstructed = document.Rewrite(
                replacements,
                cancellationToken,
                traversalCheckpoint,
                rewriteSortCheckpoint);
            using (BoundedJsonDocument validated = BoundedJsonDocument.Inspect(reconstructed, cancellationToken))
            {
                if (validated.ContainsProtectedMember)
                {
                    throw new PayloadProtectionFormatException();
                }
            }

            while (clearedPlaintextBuffers < plaintextBuffers.Count)
            {
                Clear(plaintextBuffers[clearedPlaintextBuffers], SensitiveBufferKind.DecryptedPlaintext);
                clearedPlaintextBuffers++;
                cancellationToken.ThrowIfCancellationRequested();
            }

            cancellationToken.ThrowIfCancellationRequested();
            CoreUnprotectionResult result = CoreUnprotectionResult.Readable(reconstructed);
            reconstructed = null;
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return result;
        }
        catch (OperationCanceledException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.Cancelled;
            throw;
        }
        catch (PayloadProtectionAuthenticationException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.AuthenticationFailed;
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
        catch (PayloadProtectionCryptographicException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.CryptographicFailure;
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
        }
        catch (Exception exception) when (exception is PayloadProtectionFormatException or OverflowException)
        {
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
        finally
        {
            if (reconstructed is not null)
            {
                Clear(reconstructed, SensitiveBufferKind.AbandonedOutput);
            }

            for (int index = clearedPlaintextBuffers; plaintextBuffers is not null && index < plaintextBuffers.Count; index++)
            {
                Clear(plaintextBuffers[index], SensitiveBufferKind.DecryptedPlaintext);
            }

            if (dek is not null)
            {
                Clear(dek, SensitiveBufferKind.DataEncryptionKey);
            }

            if (wrappers is not null)
            {
                ClearWrappers(wrappers);
            }

            ClearManifest(manifest);

            PayloadProtectionDiagnostics.Stop(activity, diagnosticResult);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Unprotect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Authenticates and returns one root-manifest snapshot plaintext without Server registry integration.
    /// </summary>
    /// <param name="protectedSnapshot">The validated durable snapshot carrier.</param>
    /// <param name="context">The trusted authenticated snapshot occurrence.</param>
    /// <param name="keyResolver">
    /// Resolves an exact key reference and version. Ownership of every returned non-null mutable DEK transfers to the core,
    /// which clears it on every exit.
    /// </param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The complete authenticated snapshot JSON or one bounded unreadable reason.</returns>
    internal async ValueTask<CoreUnprotectionResult> TryUnprotectSnapshotAsync(
        ProtectedSnapshotPayloadV2 protectedSnapshot,
        PayloadProtectionContext context,
        Func<string, uint, CancellationToken, ValueTask<byte[]?>> keyResolver,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectedSnapshot);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(keyResolver);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Unprotect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        byte[]? dek = null;
        byte[]? plaintext = null;
        PayloadProtectionEnvelope? envelope = null;
        ProtectedPathManifest? manifest = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Snapshot);
            if (!string.Equals(
                    protectedSnapshot.Format,
                    PayloadProtectionWireFormat.ProtectedSerializationFormat,
                    StringComparison.Ordinal)
                || !string.Equals(protectedSnapshot.SnapshotTypeId, context.PayloadTypeId, StringComparison.Ordinal)
                || protectedSnapshot.Envelope is null
                || protectedSnapshot.Envelope.Length > PayloadProtectionLimits.EnvelopeTextCharacters)
            {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            byte[] envelopeBytes = Base64UrlCodec.Decode(protectedSnapshot.Envelope);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                envelope = EnvelopeCodec.Read(envelopeBytes);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                Clear(envelopeBytes, SensitiveBufferKind.ProtectedOutput);
            }

            if (envelope.FieldOrdinal != 0)
            {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            manifest = ProtectedPathManifestCodec.Create(
                [string.Empty],
                snapshot: true,
                cancellationToken: cancellationToken);
            _ = AadCodec.Validate(
                context,
                string.Empty,
                envelope.KeyReference,
                envelope.DekVersion,
                0,
                manifest.Commitment,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            PayloadCryptography.EnsurePlatformSupport();
            try
            {
                dek = await keyResolver(envelope.KeyReference, envelope.DekVersion, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnosticResult = PayloadProtectionDiagnosticResult.Unavailable;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (dek is null)
            {
                diagnosticResult = PayloadProtectionDiagnosticResult.MissingKey;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.MissingKey);
            }

            if (dek.Length != 32)
            {
                diagnosticResult = PayloadProtectionDiagnosticResult.ConsistencyMismatch;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ConsistencyMismatch);
            }

            byte[] aad = AadCodec.Write(
                context,
                string.Empty,
                envelope.KeyReference,
                envelope.DekVersion,
                0,
                manifest.Commitment,
                cancellationToken);
            try
            {
                plaintext = PayloadCryptography.Decrypt(envelope, aad, dek, observer);
            }
            finally
            {
                Clear(aad, SensitiveBufferKind.AuthenticatedData);
            }

            if (!PayloadCryptography.HasExpectedNonce(envelope))
            {
                throw new PayloadProtectionFormatException();
            }

            using (BoundedJsonDocument validated = BoundedJsonDocument.Inspect(plaintext, cancellationToken))
            {
                if (validated.ContainsProtectedMember)
                {
                    throw new PayloadProtectionFormatException();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            CoreUnprotectionResult result = CoreUnprotectionResult.Readable(plaintext);
            plaintext = null;
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return result;
        }
        catch (OperationCanceledException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.Cancelled;
            throw;
        }
        catch (PayloadProtectionAuthenticationException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.AuthenticationFailed;
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
        catch (PayloadProtectionCryptographicException)
        {
            diagnosticResult = PayloadProtectionDiagnosticResult.CryptographicFailure;
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
        }
        catch (Exception exception) when (exception is PayloadProtectionFormatException or OverflowException)
        {
            return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
        }
        finally
        {
            if (plaintext is not null)
            {
                Clear(plaintext, SensitiveBufferKind.DecryptedPlaintext);
            }

            if (dek is not null)
            {
                Clear(dek, SensitiveBufferKind.DataEncryptionKey);
            }

            if (envelope is not null)
            {
                ClearEnvelope(envelope);
            }

            ClearManifest(manifest);

            PayloadProtectionDiagnostics.Stop(activity, diagnosticResult);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Unprotect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private PayloadProtectionMaterial? InvokeMaterialFactory(
        Func<PayloadProtectionMaterial> materialFactory,
        CancellationToken cancellationToken)
    {
        PayloadProtectionMaterial? material;
        try
        {
            material = materialFactory();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new PayloadProtectionCryptographicException();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            if (material?.DataEncryptionKey is not null)
            {
                Clear(material.DataEncryptionKey, SensitiveBufferKind.DataEncryptionKey);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        return material;
    }

    private static PayloadProtectionMaterial ValidateMaterial(PayloadProtectionMaterial? material)
    {
        if (material is null
            || !CanonicalUlid.IsValid(material.KeyReference)
            || material.DekVersion == 0
            || material.DataEncryptionKey is null
            || material.DataEncryptionKey.Length != 32)
        {
            throw new PayloadProtectionCryptographicException();
        }

        return material;
    }

    private static byte[] CreateWrapper(string encodedEnvelope)
    {
        int encodedLength = Encoding.ASCII.GetByteCount(encodedEnvelope);
        byte[] result = new byte[checked(WrapperPrefix.Length + encodedLength + WrapperSuffix.Length)];
        try
        {
            WrapperPrefix.CopyTo(result);
            Encoding.ASCII.GetBytes(encodedEnvelope, result.AsSpan(WrapperPrefix.Length, encodedLength));
            WrapperSuffix.CopyTo(result.AsSpan(WrapperPrefix.Length + encodedLength));
            return result;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(result);
            throw;
        }
    }

    private static int GetWrapperLength(int plaintextLength)
    {
        int envelopeLength = checked(PayloadProtectionWireFormat.EnvelopeFixedOverheadBytes + plaintextLength);
        return checked(WrapperPrefix.Length + Base64UrlCodec.GetEncodedLength(envelopeLength) + WrapperSuffix.Length);
    }

    private static void CheckCancellation(
        int zeroBasedIndex,
        CancellationToken cancellationToken,
        Action<int>? checkpoint)
    {
        if (zeroBasedIndex == 0 || (zeroBasedIndex & 255) == 255)
        {
            int examined = checked(zeroBasedIndex + 1);
            checkpoint?.Invoke(examined);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void ClearWrappers(IReadOnlyList<ProtectedWrapper> wrappers)
    {
        for (int index = 0; index < wrappers.Count; index++)
        {
            ClearEnvelope(wrappers[index].Envelope);
        }
    }

    private void ClearManifest(ProtectedPathManifest? manifest)
    {
        if (manifest is null)
        {
            return;
        }

        Clear(manifest.Encoded, SensitiveBufferKind.AuthenticatedData);
        Clear(manifest.Commitment, SensitiveBufferKind.AuthenticatedData);
    }

    private void ClearEnvelope(PayloadProtectionEnvelope envelope)
    {
        Clear(envelope.Nonce, SensitiveBufferKind.ProtectedOutput);
        Clear(envelope.Ciphertext, SensitiveBufferKind.ProtectedOutput);
        Clear(envelope.Tag, SensitiveBufferKind.ProtectedOutput);
    }

    private void Clear(byte[] buffer, SensitiveBufferKind kind)
    {
        CryptographicOperations.ZeroMemory(buffer);
        if (observer is null)
        {
            return;
        }

        try
        {
            observer.BufferCleared(kind, buffer);
        }
        catch
        {
            // Test-only observation is best effort and cannot alter cleanup or operation outcomes.
        }
    }
}
