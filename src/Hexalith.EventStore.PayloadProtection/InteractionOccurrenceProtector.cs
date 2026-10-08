using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Private occurrence-safe candidate composition around the unchanged normative v2 core.
/// It grants no production profile/credential authority; registry, exclusive invocation lease and exact root/DigestKey custody are all mandatory.</summary>
internal sealed class InteractionOccurrenceProtector(IInteractionOccurrenceRegistry registry, IInteractionOccurrenceKeyProvider keys, TimeProvider clock)
{
    private readonly PayloadProtectionCore _core = new();

    /// <summary>Protects one exact selected-field event after durable uniqueness and exclusive encryption lease; retries return only original ciphertext.</summary>
    internal Task<InteractionOccurrenceSealedResult?> ProtectEventAsync(byte[] payload, IReadOnlyCollection<string> paths,
        InteractionOccurrenceIdentity identity, string digestVersion, long attempt, string proposedReference, CancellationToken token = default)
        => ProtectAsync(payload, paths, identity, digestVersion, attempt, proposedReference, token);

    /// <summary>Protects one exact whole snapshot through the same root/occurrence registry; the retained bytes are the complete v2 snapshot carrier.</summary>
    internal Task<InteractionOccurrenceSealedResult?> ProtectSnapshotAsync(byte[] payload, InteractionOccurrenceIdentity identity,
        string digestVersion, long attempt, string proposedReference, CancellationToken token = default)
        => ProtectAsync(payload, new[] { string.Empty }, identity, digestVersion, attempt, proposedReference, token);

    /// <summary>Unprotects only an independently committed active original occurrence. Pending/unknown/erased root yields no plaintext.</summary>
    internal async Task<CoreUnprotectionResult> UnprotectAsync(InteractionOccurrenceIdentity identity, CancellationToken token = default)
    {
        long start = clock.GetTimestamp(); byte[]? abandonedPlaintext = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var result = await AwaitAsync(() => registry.LookupAsync(identity, CancellationToken.None), start, token).ConfigureAwait(false);
            var record = result.Record;
            if (result.Status != InteractionOccurrenceReservationStatus.Sealed || record?.Request.Identity != identity
                || record.WriterState != InteractionOccurrenceWriterState.Active || record.Sealed is null)
            { return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable); }
            var sealedResult = Copy(record.Sealed);
            if (sealedResult.KeyReference != record.KeyReference || !CanonicalUlid.IsValid(record.KeyReference))
            { return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.BytesMetadataMismatch); }
            using var root = await AwaitAsync(() => keys.ResolveRootAsync(identity, record.KeyReference, false, CancellationToken.None), start, token,
                static owned => owned?.Dispose()).ConfigureAwait(false);
            if (!ValidKey(root, identity, "interaction-root", identity.RootKeyVersion))
            { return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.KeyInvalidatedOrDeleted); }
            var context = Context(identity); CoreUnprotectionResult read;
            if (identity.Kind == PayloadProtectionPayloadKind.Event)
            {
                read = await _core.TryUnprotectEventAsync(sealedResult.PayloadBytes, context,
                    (reference, version, cancellation) => ResolveDerived(root!, identity, record.KeyReference, reference, version, cancellation), token).ConfigureAwait(false);
            }
            else
            {
                var snapshot = ReadSnapshot(sealedResult.PayloadBytes);
                read = await _core.TryUnprotectSnapshotAsync(snapshot, context,
                    (reference, version, cancellation) => ResolveDerived(root!, identity, record.KeyReference, reference, version, cancellation), token).ConfigureAwait(false);
            }
            abandonedPlaintext = read.PayloadBytes;
            if (!await AwaitAsync(() => keys.IsCurrentAsync(identity, "Read", CancellationToken.None), start, token).ConfigureAwait(false))
            {
                if (read.PayloadBytes is not null) { CryptographicOperations.ZeroMemory(read.PayloadBytes); }
                return CoreUnprotectionResult.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable);
            }
            CheckBudget(start, token); abandonedPlaintext = null; return read;
        }
        catch (Exception) { token.ThrowIfCancellationRequested(); throw; }
        finally { if (abandonedPlaintext is not null) { CryptographicOperations.ZeroMemory(abandonedPlaintext); } }
    }

    /// <summary>Records exact independent source writer completion; ciphertext alone never establishes committed replay/caches.</summary>
    internal async Task<InteractionOccurrenceReservationResult> CompleteWriterAsync(InteractionOccurrenceIdentity identity,
        string keyReference, string proofId, bool persisted, CancellationToken token = default)
    {
        long start = clock.GetTimestamp();
        try { return await AwaitAsync(() => registry.CompleteWriterAsync(identity, keyReference, proofId, persisted, CancellationToken.None), start, token).ConfigureAwait(false); }
        catch (Exception) { token.ThrowIfCancellationRequested(); throw; }
    }

    private async Task<InteractionOccurrenceSealedResult?> ProtectAsync(byte[] payload, IReadOnlyCollection<string> paths,
        InteractionOccurrenceIdentity identity, string digestVersion, long attempt, string proposedReference, CancellationToken token)
    {
        long start = clock.GetTimestamp(); byte[]? owned = null; PayloadProtectionMaterial? material = null; ProtectedPathManifest? manifest = null;
        try
        {
            token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(payload); ArgumentNullException.ThrowIfNull(paths); ArgumentNullException.ThrowIfNull(identity);
            if (payload.Length == 0 || payload.Length > PayloadProtectionLimits.PayloadBytes || attempt < 1 || !CanonicalUlid.IsValid(proposedReference))
            { throw new PayloadProtectionFormatException(); }
            AadCodec.ValidateContext(Context(identity), identity.Kind);
            owned = payload.ToArray();
            manifest = ProtectedPathManifestCodec.Create(paths, snapshot: identity.Kind == PayloadProtectionPayloadKind.Snapshot, cancellationToken: token);
            using var digest = await AwaitAsync(() => keys.ResolveDigestAsync(identity, digestVersion, CancellationToken.None), start, token,
                static key => key?.Dispose()).ConfigureAwait(false);
            if (!ValidKey(digest, identity, "tenant-digest", digestVersion)) { return null; }
            string fingerprint = Fingerprint(digest!.Bytes, identity, manifest.Encoded, owned);
            var request = new InteractionOccurrenceRequest(identity, digestVersion, fingerprint, attempt, proposedReference);
            var reserved = await AwaitAsync(() => registry.ReserveAsync(request, CancellationToken.None), start, token).ConfigureAwait(false);
            var record = reserved.Record;
            if (record is null || record.Request.Identity != identity || record.Request.DigestKeyVersion != digestVersion
                || record.Request.ContentIntentHmac != fingerprint || record.Request.ReservationAttemptOrdinal != attempt
                || !CanonicalUlid.IsValid(record.KeyReference)) { return null; }
            if (reserved.Status == InteractionOccurrenceReservationStatus.Sealed && record.Sealed is not null)
            { return await ReleaseAsync(identity, record, start, token).ConfigureAwait(false); }
            if (reserved.Status != InteractionOccurrenceReservationStatus.Reserved || record.WriterState != InteractionOccurrenceWriterState.Reserved
                || record.Sealed is not null || !await AwaitAsync(() => keys.AcquireEncryptionLeaseAsync(record, CancellationToken.None), start, token).ConfigureAwait(false))
            { return null; }
            using var root = await AwaitAsync(() => keys.ResolveRootAsync(identity, record.KeyReference, true, CancellationToken.None), start, token,
                static key => key?.Dispose()).ConfigureAwait(false);
            if (!ValidKey(root, identity, "interaction-root", identity.RootKeyVersion)) { return null; }
            material = InteractionOccurrenceKeyDeriver.Derive(root!.Bytes, identity, record.KeyReference, token);
            InteractionOccurrenceSealedResult encrypted;
            if (identity.Kind == PayloadProtectionPayloadKind.Event)
            {
                var result = _core.ProtectEvent(owned, manifest.Paths, Context(identity), () => material, cancellationToken: token);
                if (result.ProtectedPathCount == 0) { return null; }
                encrypted = new(record.KeyReference, result.PayloadBytes, result.SerializationFormat, result.ProtectedPathCount);
            }
            else
            {
                var result = _core.ProtectSnapshot(owned, Context(identity), () => material, cancellationToken: token);
                encrypted = new(record.KeyReference, WriteSnapshot(result), result.Format, 1);
            }
            CheckBudget(start, token);
            var retained = await AwaitAsync(() => registry.RetainSealedAsync(identity, encrypted, CancellationToken.None), start, token).ConfigureAwait(false);
            if (retained.Status != InteractionOccurrenceReservationStatus.Sealed || retained.Record?.Request != record.Request
                || retained.Record.Sealed is null || !Equal(retained.Record.Sealed, encrypted)) { return null; }
            return await ReleaseAsync(identity, retained.Record, start, token).ConfigureAwait(false);
        }
        catch (Exception) { token.ThrowIfCancellationRequested(); throw; }
        finally
        {
            if (owned is not null) { CryptographicOperations.ZeroMemory(owned); }
            if (material?.DataEncryptionKey is not null) { CryptographicOperations.ZeroMemory(material.DataEncryptionKey); }
            if (manifest is not null) { CryptographicOperations.ZeroMemory(manifest.Encoded); CryptographicOperations.ZeroMemory(manifest.Commitment); }
        }
    }

    private async Task<InteractionOccurrenceSealedResult?> ReleaseAsync(InteractionOccurrenceIdentity identity, InteractionOccurrenceRecord record, long start, CancellationToken token)
    {
        var result = Copy(record.Sealed!);
        if (result.KeyReference != record.KeyReference || !await AwaitAsync(() => keys.IsCurrentAsync(identity, "Write", CancellationToken.None), start, token).ConfigureAwait(false)) { return null; }
        CheckBudget(start, token); return result;
    }

    private static bool ValidKey(InteractionOccurrenceOwnedKey? key, InteractionOccurrenceIdentity identity, string purpose, string version)
        => key is not null && key.Bytes is { Length: 32 } && key.Target == identity.Target && key.Purpose == purpose && key.Version == version;
    private static PayloadProtectionContext Context(InteractionOccurrenceIdentity identity) => new(identity.Owner, identity.PayloadTypeId, identity.Kind, identity.Sequence);
    private static ValueTask<byte[]?> ResolveDerived(InteractionOccurrenceOwnedKey root, InteractionOccurrenceIdentity identity, string expectedReference,
        string reference, uint version, CancellationToken token)
        => ValueTask.FromResult<byte[]?>(reference == expectedReference && version == 1
            ? InteractionOccurrenceKeyDeriver.Derive(root.Bytes.ToArray(), identity, reference, token).DataEncryptionKey : null);
    private static InteractionOccurrenceSealedResult Copy(InteractionOccurrenceSealedResult result)
    {
        if (result.PayloadBytes is null || result.PayloadBytes.Length > PayloadProtectionLimits.PayloadBytes || result.ProtectedPathCount is < 1 or > PayloadProtectionLimits.ProtectedPaths
            || result.SerializationFormat != PayloadProtectionWireFormat.ProtectedSerializationFormat) { throw new PayloadProtectionFormatException(); }
        return result with { PayloadBytes = result.PayloadBytes.ToArray() };
    }
    private static bool Equal(InteractionOccurrenceSealedResult first, InteractionOccurrenceSealedResult second)
        => first.KeyReference == second.KeyReference && first.SerializationFormat == second.SerializationFormat && first.ProtectedPathCount == second.ProtectedPathCount
            && first.PayloadBytes.AsSpan().SequenceEqual(second.PayloadBytes);

    private static string Fingerprint(byte[] digestKey, InteractionOccurrenceIdentity identity, byte[] manifest, byte[] payload)
    {
        // Only this retained-key HMAC covers plaintext. Registry state contains the resulting keyed digest, never raw content or an unkeyed content hash.
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, digestKey);
        hmac.AppendData("HXOC.candidate.v1"u8);
        string[] fields = [identity.Target.TenantId, identity.Target.AgentInteractionId, identity.Target.TargetProtectionKeyAlias,
            identity.Owner.TenantId, identity.Owner.Domain, identity.Owner.AggregateId, identity.PayloadTypeId, identity.RootKeyVersion,
            identity.DerivationProfileVersion, ((byte)identity.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture), identity.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        foreach (string field in fields) { byte[] bytes = CanonicalText.Encode(field, 1, 2048); try { Append(hmac, bytes); } finally { CryptographicOperations.ZeroMemory(bytes); } }
        Append(hmac, manifest); Append(hmac, payload); byte[] result = hmac.GetHashAndReset();
        try { return Convert.ToHexString(result).ToLowerInvariant(); } finally { CryptographicOperations.ZeroMemory(result); }
    }
    private static void Append(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    { Span<byte> length = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length)); hash.AppendData(length); hash.AppendData(bytes); }

    private static byte[] WriteSnapshot(ProtectedSnapshotPayloadV2 snapshot)
    {
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject(); writer.WriteString("Format", snapshot.Format); writer.WriteString("SnapshotTypeId", snapshot.SnapshotTypeId); writer.WriteString("Envelope", snapshot.Envelope); writer.WriteEndObject(); writer.Flush(); return stream.ToArray();
    }
    private static ProtectedSnapshotPayloadV2 ReadSnapshot(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes); var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 3) { throw new PayloadProtectionFormatException(); }
        return new(value.GetProperty("Format").GetString()!, value.GetProperty("SnapshotTypeId").GetString()!, value.GetProperty("Envelope").GetString()!);
    }
    private void CheckBudget(long start, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); if (clock.GetElapsedTime(start) >= TimeSpan.FromSeconds(30)) { throw new TimeoutException("Occurrence operation unavailable."); }
    }
    private async Task<T> AwaitAsync<T>(Func<Task<T>> operation, long start, CancellationToken token, Action<T>? abandoned = null)
    {
        CheckBudget(start, token); var pending = Task.Run(operation, CancellationToken.None);
        try
        {
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(30) - clock.GetElapsedTime(start), clock, token).ConfigureAwait(false);
            try { CheckBudget(start, token); return result; } catch { abandoned?.Invoke(result); throw; }
        }
        catch (Exception)
        {
            // Late owned material is cleared away from the query continuation; exceptions are observed without releasing evidence.
            _ = pending.ContinueWith(task =>
            {
                try { if (task.IsCompletedSuccessfully) { abandoned?.Invoke(task.Result); } else { _ = task.Exception; } } catch (Exception) { /* Cleanup failure is observed; it cannot release evidence. */ }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            token.ThrowIfCancellationRequested(); throw;
        }
    }
}
