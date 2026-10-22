using System.Buffers.Binary;
using System.Security.Cryptography;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Private candidate HKDF hierarchy, preserving the v2 fresh-key nonce precondition without changing normative core bytes.</summary>
internal static class InteractionOccurrenceKeyDeriver
{
    /// <summary>Derives one ephemeral unique-reference occurrence subkey. Root ownership transfers here and is cleared on every exit.
    /// Only an independently authenticated durably unique reference/current completion lease permits invocation; this pure primitive grants neither.</summary>
    internal static PayloadProtectionMaterial Derive(byte[] ownedRoot, InteractionOccurrenceIdentity identity, string uniqueKeyReference, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(ownedRoot); byte[]? info = null; byte[]? derived = null;
        try
        {
            token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(identity); ArgumentNullException.ThrowIfNull(identity.Target); ArgumentNullException.ThrowIfNull(identity.Owner);
            if (ownedRoot.Length != 32 || identity.Target.TenantId != identity.Owner.TenantId || identity.Sequence == 0
                || identity.DerivationProfileVersion != "candidate-hkdf-sha256-v1" || !CanonicalUlid.IsValid(uniqueKeyReference)) { throw new PayloadProtectionFormatException(); }
            var context = new PayloadProtectionContext(identity.Owner, identity.PayloadTypeId, identity.Kind, identity.Sequence); AadCodec.ValidateContext(context, identity.Kind);
            var fields = new[] { identity.DerivationProfileVersion, identity.Target.TenantId, identity.Target.AgentInteractionId, identity.Target.TargetProtectionKeyAlias,
                identity.RootKeyVersion, identity.Owner.Domain, identity.Owner.AggregateId, identity.PayloadTypeId, uniqueKeyReference };
            var encoded = new List<byte[]>();
            try
            {
                foreach (string field in fields) { token.ThrowIfCancellationRequested(); encoded.Add(CanonicalText.Encode(field, 1, 2048)); }
                info = new byte[checked(4 + 1 + 8 + encoded.Sum(f => 4 + f.Length))]; "HXOK"u8.CopyTo(info); info[4] = (byte)identity.Kind;
                BinaryPrimitives.WriteUInt64BigEndian(info.AsSpan(5, 8), identity.Sequence); int offset = 13;
                foreach (byte[] field in encoded) { BinaryPrimitives.WriteUInt32BigEndian(info.AsSpan(offset, 4), checked((uint)field.Length)); offset += 4; field.CopyTo(info, offset); offset += field.Length; }
                derived = HKDF.DeriveKey(HashAlgorithmName.SHA256, ownedRoot, 32, "Hexalith.candidate.interaction-occurrence.v1"u8.ToArray(), info);
                token.ThrowIfCancellationRequested(); var result = new PayloadProtectionMaterial(uniqueKeyReference, 1, derived); derived = null; return result;
            }
            finally { foreach (byte[] field in encoded) { CryptographicOperations.ZeroMemory(field); } }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownedRoot); if (info is not null) { CryptographicOperations.ZeroMemory(info); }
            if (derived is not null) { CryptographicOperations.ZeroMemory(derived); }
        }
    }
}
