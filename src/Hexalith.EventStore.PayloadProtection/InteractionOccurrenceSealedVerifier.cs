using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Authenticates native sealed output and its retained keyed content intent without releasing plaintext. Owns and clears the supplied root on every path.</summary>
public static class InteractionOccurrenceSealedVerifier
{
    /// <summary>Verifies the actual v2 AEAD/context/reference and exact original plaintext intent; missing or substituted material returns false.</summary>
    public static async Task<bool> VerifyAsync(InteractionOccurrenceRequest original, InteractionOccurrenceSealedResult sealedResult,
        byte[] ownedRoot, byte[] digestKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(sealedResult);
        ArgumentNullException.ThrowIfNull(ownedRoot);
        ArgumentNullException.ThrowIfNull(digestKey);
        PayloadProtectionMaterial? material = null; ProtectedPathManifest? manifest = null; byte[]? clear = null;
        try
        {
            if (ownedRoot.Length != 32 || digestKey.Length != 32 || sealedResult.KeyReference != original.ProposedKeyReference
                || sealedResult.SerializationFormat != "json+pdenc-v2" || sealedResult.ProtectedPathCount != 1) { return false; }
            var identity = original.Identity;
            material = InteractionOccurrenceKeyDeriver.Derive(ownedRoot, identity, sealedResult.KeyReference, cancellationToken);
            ValueTask<byte[]?> Resolve(string reference, uint version, CancellationToken token)
            { token.ThrowIfCancellationRequested(); return ValueTask.FromResult<byte[]?>(reference == sealedResult.KeyReference && version == 1 ? material.DataEncryptionKey.ToArray() : null); }
            var context = new PayloadProtectionContext(identity.Owner, identity.PayloadTypeId, identity.Kind, identity.Sequence);
            var core = new PayloadProtectionCore();
            CoreUnprotectionResult opened;
            if (identity.Kind == PayloadProtectionPayloadKind.Event)
            {
                manifest = ProtectedPathManifestCodec.Create(["/ProtectedContent/Value"], cancellationToken: cancellationToken);
                opened = await core.TryUnprotectEventAsync(sealedResult.PayloadBytes, context, Resolve, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                manifest = ProtectedPathManifestCodec.Create([string.Empty], snapshot: true, cancellationToken: cancellationToken);
                var snapshot = JsonSerializer.Deserialize(sealedResult.PayloadBytes, InteractionSnapshotJsonContext.Default.ProtectedSnapshotPayloadV2) ?? throw new PayloadProtectionFormatException();
                opened = await core.TryUnprotectSnapshotAsync(snapshot, context, Resolve, cancellationToken).ConfigureAwait(false);
            }
            clear = opened.PayloadBytes;
            if (!opened.IsReadable || clear is null) { return false; }
            if (sealedResult.SnapshotReplaySkeleton is { } skeleton)
            {
                if (identity.Kind != PayloadProtectionPayloadKind.Snapshot) { return false; }
                var layout = SnapshotEntryLayout.Read(clear, identity.Target.TenantId, false, cancellationToken);
                if (layout.Entries.Length == 0 || !layout.Skeleton.AsSpan().SequenceEqual(skeleton)
                    || layout.Entries.Any(entry => entry.Target != identity.Target)) { return false; }
            }
            byte[] actual = Convert.FromHexString(InteractionOccurrenceProtector.Fingerprint(digestKey, identity, manifest.Encoded, clear));
            byte[] expected = Convert.FromHexString(original.ContentIntentHmac);
            try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
            finally { CryptographicOperations.ZeroMemory(actual); CryptographicOperations.ZeroMemory(expected); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
        finally
        {
            if (clear is not null) { CryptographicOperations.ZeroMemory(clear); }
            if (material?.DataEncryptionKey is not null) { CryptographicOperations.ZeroMemory(material.DataEncryptionKey); }
            if (manifest is not null) { CryptographicOperations.ZeroMemory(manifest.Encoded); CryptographicOperations.ZeroMemory(manifest.Commitment); }
            CryptographicOperations.ZeroMemory(ownedRoot);
        }
    }
}
