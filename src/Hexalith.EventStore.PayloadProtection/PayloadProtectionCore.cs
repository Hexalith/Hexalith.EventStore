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
    internal CoreProtectionResult ProtectEvent(
        byte[] payloadBytes,
        IReadOnlyCollection<string> selectedPaths,
        PayloadProtectionContext context,
        Func<PayloadProtectionMaterial> materialFactory,
        int maximumProtectedValueBytes = PayloadProtectionLimits.CiphertextBytes,
        CancellationToken cancellationToken = default,
        Action<int>? encryptionCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentNullException.ThrowIfNull(selectedPaths);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(materialFactory);
        Stopwatch stopwatch = Stopwatch.StartNew();
        Activity? activity = PayloadProtectionDiagnostics.Start(PayloadProtectionOperation.Protect);
        PayloadProtectionDiagnosticResult diagnosticResult = PayloadProtectionDiagnosticResult.Malformed;
        PayloadProtectionMaterial? material = null;
        List<(BoundedJsonNode Node, byte[] Plaintext)>? selectedValues = null;
        List<JsonReplacement>? replacements = null;
        byte[]? transformed = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maximumProtectedValueBytes is < 1 or > PayloadProtectionLimits.CiphertextBytes)
            {
                throw new PayloadProtectionFormatException();
            }

            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Event);
            ProtectedPathManifest requestedManifest = ProtectedPathManifestCodec.Create(
                selectedPaths,
                cancellationToken: cancellationToken,
                allowEmpty: true);
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(
                payloadBytes,
                cancellationToken,
                observer: observer,
                ownedBufferKind: SensitiveBufferKind.InputSnapshot);
            if (document.ContainsProtectedMember)
            {
                throw new PayloadProtectionFormatException();
            }

            if (requestedManifest.Paths.Count == 0)
            {
                byte[] passThrough = document.CopyPayload(cancellationToken);
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return new CoreProtectionResult(passThrough, "json", 0);
            }

            selectedValues = new List<(BoundedJsonNode Node, byte[] Plaintext)>(requestedManifest.Paths.Count);
            var nonNullPaths = new List<string>(requestedManifest.Paths.Count);
            long totalPlaintext = 0;
            for (int index = 0; index < requestedManifest.Paths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = requestedManifest.Paths[index];
                BoundedJsonNode node = document.Resolve(path);
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

                byte[] plaintext = document.CopyRawValue(node);
                selectedValues.Add((node, plaintext));
                nonNullPaths.Add(path);
            }

            if (nonNullPaths.Count == 0)
            {
                byte[] passThrough = document.CopyPayload(cancellationToken);
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return new CoreProtectionResult(passThrough, "json", 0);
            }

            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(
                nonNullPaths,
                cancellationToken: cancellationToken);
            material = InvokeMaterialFactory(materialFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateMaterial(material);
            replacements = new List<JsonReplacement>(manifest.Paths.Count);
            for (int index = 0; index < manifest.Paths.Count; index++)
            {
                encryptionCheckpoint?.Invoke(index);
                cancellationToken.ThrowIfCancellationRequested();
                string path = manifest.Paths[index];
                if (!string.Equals(nonNullPaths[index], path, StringComparison.Ordinal))
                {
                    throw new PayloadProtectionFormatException();
                }

                (BoundedJsonNode node, byte[] plaintext) = selectedValues[index];
                byte[] aad = AadCodec.Write(
                    context,
                    path,
                    material.KeyReference,
                    material.DekVersion,
                    checked((uint)index),
                    manifest.Commitment);
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
                    byte[] wrapper = CreateWrapper(encodedEnvelope);
                    replacements.Add(new JsonReplacement(node.Start, node.Length, wrapper));
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

            transformed = document.Rewrite(replacements, cancellationToken);
            using (BoundedJsonDocument validated = BoundedJsonDocument.Inspect(transformed, cancellationToken))
            {
                _ = validated.NodeCount;
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] transferred = transformed;
            transformed = null;
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return new CoreProtectionResult(transferred, "json+pdenc-v2", manifest.Paths.Count);
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

            PayloadProtectionDiagnostics.Stop(activity);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Protect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Protects one complete snapshot as the root-path v2 envelope without Server or registry integration.
    /// </summary>
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
            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(
                [string.Empty],
                snapshot: true,
                cancellationToken: cancellationToken);
            material = InvokeMaterialFactory(materialFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateMaterial(material);
            byte[] aad = AadCodec.Write(
                context,
                string.Empty,
                material.KeyReference,
                material.DekVersion,
                0,
                manifest.Commitment);
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
                diagnosticResult = PayloadProtectionDiagnosticResult.Success;
                return new ProtectedSnapshotPayloadV2("json+pdenc-v2", context.PayloadTypeId, encodedEnvelope);
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

            PayloadProtectionDiagnostics.Stop(activity);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Protect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Authenticates every event wrapper before transferring any reconstructed plaintext.
    /// </summary>
    internal async ValueTask<CoreUnprotectionResult> TryUnprotectEventAsync(
        byte[] protectedPayloadBytes,
        PayloadProtectionContext context,
        Func<string, uint, CancellationToken, ValueTask<byte[]?>> keyResolver,
        CancellationToken cancellationToken = default)
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
        var plaintextBuffers = new List<byte[]>();
        int clearedPlaintextBuffers = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Event);
            using BoundedJsonDocument document = BoundedJsonDocument.Parse(
                protectedPayloadBytes,
                cancellationToken,
                observer: observer,
                ownedBufferKind: SensitiveBufferKind.InputSnapshot);
            wrappers = document.ReadProtectedWrappers(cancellationToken);
            if (wrappers.Count is < 1 or > PayloadProtectionLimits.ProtectedPaths)
            {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            string[] wrapperPaths = new string[wrappers.Count];
            for (int index = 0; index < wrappers.Count; index++)
            {
                wrapperPaths[index] = wrappers[index].Path;
            }

            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(
                wrapperPaths,
                cancellationToken: cancellationToken);
            ProtectedWrapper? first = null;
            var wrappersByPath = new Dictionary<string, ProtectedWrapper>(wrappers.Count, StringComparer.Ordinal);
            for (int index = 0; index < wrappers.Count; index++)
            {
                if (!wrappersByPath.TryAdd(wrappers[index].Path, wrappers[index]))
                {
                    return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
                }
            }

            var orderedWrappers = new ProtectedWrapper[wrappers.Count];
            for (int index = 0; index < manifest.Paths.Count; index++)
            {
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
                    manifest.Commitment);
            }

            cancellationToken.ThrowIfCancellationRequested();
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
            catch (OperationCanceledException)
            {
                diagnosticResult = PayloadProtectionDiagnosticResult.Unavailable;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
            }
            catch
            {
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
            long totalPlaintext = 0;
            for (int index = 0; index < orderedWrappers.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProtectedWrapper wrapper = orderedWrappers[index];
                byte[] aad = AadCodec.Write(
                    context,
                    wrapper.Path,
                    wrapper.Envelope.KeyReference,
                    wrapper.Envelope.DekVersion,
                    wrapper.Envelope.FieldOrdinal,
                    manifest.Commitment);
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

                totalPlaintext = checked(totalPlaintext + plaintext.Length);
                if (totalPlaintext > PayloadProtectionLimits.SelectedPlaintextBytes)
                {
                    throw new PayloadProtectionFormatException();
                }

                using (BoundedJsonDocument plaintextDocument = BoundedJsonDocument.Inspect(plaintext, cancellationToken))
                {
                    if (plaintextDocument.ContainsProtectedMember)
                    {
                        throw new PayloadProtectionFormatException();
                    }
                }

                replacements.Add(new JsonReplacement(wrapper.Start, wrapper.Length, plaintext));
                cancellationToken.ThrowIfCancellationRequested();
            }

            reconstructed = document.Rewrite(replacements, cancellationToken);
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
            byte[] transferred = reconstructed;
            reconstructed = null;
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return CoreUnprotectionResult.Readable(transferred);
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

            for (int index = clearedPlaintextBuffers; index < plaintextBuffers.Count; index++)
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

            PayloadProtectionDiagnostics.Stop(activity);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Unprotect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Authenticates and returns one root-manifest snapshot plaintext without Server registry integration.
    /// </summary>
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
        byte[]? output = null;
        PayloadProtectionEnvelope? envelope = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            AadCodec.ValidateContext(context, PayloadProtectionPayloadKind.Snapshot);
            if (!string.Equals(protectedSnapshot.Format, "json+pdenc-v2", StringComparison.Ordinal)
                || !string.Equals(protectedSnapshot.SnapshotTypeId, context.PayloadTypeId, StringComparison.Ordinal)
                || protectedSnapshot.Envelope is null
                || protectedSnapshot.Envelope.Length > PayloadProtectionLimits.EnvelopeTextCharacters)
            {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            byte[] envelopeBytes = Base64UrlCodec.Decode(protectedSnapshot.Envelope);
            try
            {
                envelope = EnvelopeCodec.Read(envelopeBytes);
            }
            finally
            {
                Clear(envelopeBytes, SensitiveBufferKind.ProtectedOutput);
            }

            if (envelope.FieldOrdinal != 0)
            {
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch);
            }

            ProtectedPathManifest manifest = ProtectedPathManifestCodec.Create(
                [string.Empty],
                snapshot: true,
                cancellationToken: cancellationToken);
            _ = AadCodec.Validate(
                context,
                string.Empty,
                envelope.KeyReference,
                envelope.DekVersion,
                0,
                manifest.Commitment);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                dek = await keyResolver(envelope.KeyReference, envelope.DekVersion, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                diagnosticResult = PayloadProtectionDiagnosticResult.Unavailable;
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
            }
            catch
            {
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

            byte[] aad = AadCodec.Write(context, string.Empty, envelope.KeyReference, envelope.DekVersion, 0, manifest.Commitment);
            try
            {
                plaintext = PayloadCryptography.Decrypt(envelope, aad, dek, observer);
            }
            finally
            {
                Clear(aad, SensitiveBufferKind.AuthenticatedData);
            }

            if (!PayloadCryptography.HasExpectedNonce(envelope)
                || plaintext.Length > PayloadProtectionLimits.CiphertextBytes)
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

            output = plaintext.ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            byte[] transferred = output;
            output = null;
            diagnosticResult = PayloadProtectionDiagnosticResult.Success;
            return CoreUnprotectionResult.Readable(transferred);
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
            if (output is not null)
            {
                Clear(output, SensitiveBufferKind.AbandonedOutput);
            }

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

            PayloadProtectionDiagnostics.Stop(activity);
            PayloadProtectionDiagnostics.Record(
                PayloadProtectionOperation.Unprotect,
                diagnosticResult,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static PayloadProtectionMaterial InvokeMaterialFactory(
        Func<PayloadProtectionMaterial> materialFactory,
        CancellationToken cancellationToken)
    {
        PayloadProtectionMaterial material;
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
            throw new PayloadProtectionCryptographicException();
        }

        return material;
    }

    private static void ValidateMaterial(PayloadProtectionMaterial material)
    {
        if (material is null
            || !CanonicalUlid.IsValid(material.KeyReference)
            || material.DekVersion == 0
            || material.DataEncryptionKey is null
            || material.DataEncryptionKey.Length != 32)
        {
            throw new PayloadProtectionCryptographicException();
        }
    }

    private static byte[] CreateWrapper(string encodedEnvelope)
    {
        int encodedLength = Encoding.ASCII.GetByteCount(encodedEnvelope);
        byte[] result = new byte[checked(WrapperPrefix.Length + encodedLength + WrapperSuffix.Length)];
        WrapperPrefix.CopyTo(result);
        Encoding.ASCII.GetBytes(encodedEnvelope, result.AsSpan(WrapperPrefix.Length, encodedLength));
        WrapperSuffix.CopyTo(result.AsSpan(WrapperPrefix.Length + encodedLength));
        return result;
    }

    private void ClearWrappers(IReadOnlyList<ProtectedWrapper> wrappers)
    {
        for (int index = 0; index < wrappers.Count; index++)
        {
            ClearEnvelope(wrappers[index].Envelope);
        }
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
