using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;
/// <summary>Pins a separately selected anchored purpose-03 model to the exact current logical route trust.</summary>
/// <remarks>Explicit dormant construction grants no production key, catalog or serving authority.</remarks>
internal sealed class DaprLogicalAnchoredClaimTrust : IDisposable
{
    private readonly DaprLogicalClaimTrust _routes;
    private readonly byte[] _spki;
    private readonly ECDsa _publicKey;
    private bool _disposed;
    /// <summary>Creates only the explicit anchored model using the current exact route key/domain/registry/loss tuple.</summary>
    internal DaprLogicalAnchoredClaimTrust(string model, DaprLogicalClaimTrust routeTrust, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (model != DaprLogicalReplayAnchorCodec.ModelId)
        {
            throw new ArgumentException("An explicit distinct anchored trust selection is required.");
        }

        _routes = routeTrust ?? throw new ArgumentNullException(nameof(routeTrust));
        _spki = _routes.CapturePublicKey(token);
        _publicKey = ECDsa.Create();
        try
        {
            _publicKey.ImportSubjectPublicKeyInfo(_spki, out int read);
            if (read != _spki.Length)
            {
                throw new CryptographicException("Exact anchored SPKI is required.");
            }

            token.ThrowIfCancellationRequested();
        }
        catch
        {
            _publicKey.Dispose();
            CryptographicOperations.ZeroMemory(_spki);
            throw;
        }
    }

    /// <summary>Checks current trust and the exact route-trust object before every anchored operation.</summary>
    internal void RequireCurrent(DaprLogicalClaimTrust routes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ReferenceEquals(routes, _routes))
        {
            throw new InvalidOperationException("AnchorCapabilityHold: exact current route trust is required.");
        }

        try
        {
            _routes.RequireCurrent(token);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }
    }

    /// <summary>Signs only a strictly decoded anchored prefix with the exact current logical key.</summary>
    internal DaprLogicalSignedClaim SignPrefix(ReadOnlySpan<byte> claim, ECDsa key, EventBufferBudget budget, CancellationToken token)
    {
        RequireCurrent(_routes, token);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(budget);
        if (claim.Length > DaprLogicalAnchoredPrefixCodec.MaximumBytes)
        {
            throw new ArgumentException("Anchored claim exceeds its ceiling.");
        }

        using EventBufferReservation working = budget.Reserve(checked(claim.Length * 4 + 4096));
        EventBufferReservation retained = budget.Reserve(checked(claim.Length + 320));
        byte[] image = claim.ToArray();
        byte[]? signature = null;
        DaprLogicalSignedClaim? captured = null;
        try
        {
            RequireScope(DaprLogicalAnchoredPrefixCodec.Decode(image));
            byte[] actual = key.ExportSubjectPublicKeyInfo();
            try
            {
                if (!actual.AsSpan().SequenceEqual(_spki))
                {
                    throw new InvalidOperationException("AnchorCapabilityHold: exact current signing key changed.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actual);
            }

            byte[] digest = SignatureHash(image);
            try
            {
                signature = key.SignHash(digest, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }

            if (signature.Length != 64)
            {
                throw new CryptographicException("Anchored signature must be fixed P1363.");
            }

            RequireCurrent(_routes, token);
            captured = new DaprLogicalSignedClaim(image, _routes.CurrentKeyId, signature, retained);
            return captured;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(image);
            if (signature is not null)
            {
                CryptographicOperations.ZeroMemory(signature);
            }

            retained.Dispose();
            throw;
        }
        finally
        {
            try
            {
                token.ThrowIfCancellationRequested();
            }
            catch
            {
                captured?.Dispose();
                throw;
            }
        }
    }

    /// <summary>Verifies the exact separate prefix and expected private selection before returning retained decoded fields.</summary>
    internal DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> VerifyPrefix(ReadOnlySpan<byte> claim, string key, ReadOnlySpan<byte> signature, DaprLogicalReplayAnchorSelection selection, ReadOnlySpan<byte> selectionHash, EventBufferBudget budget, CancellationToken token)
    {
        RequireCurrent(_routes, token);
        if (claim.Length > DaprLogicalAnchoredPrefixCodec.MaximumBytes || signature.Length != 64 || key != _routes.CurrentKeyId)
        {
            throw new InvalidOperationException("AnchorCapabilityHold: anchored proof size, signature or current key mismatch.");
        }

        EventBufferReservation decoding = budget.Reserve(checked(claim.Length * 4 + 4096));
        byte[] image = claim.ToArray();
        DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim>? captured = null;
        try
        {
            byte[] digest = SignatureHash(image);
            bool valid;
            try
            {
                valid = _publicKey.VerifyHash(digest, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }

            if (!valid)
            {
                throw new InvalidOperationException("AnchorCapabilityHold: invalid anchored signature.");
            }

            DaprLogicalAnchoredPrefixClaim value = DaprLogicalAnchoredPrefixCodec.Decode(image);
            RequireScope(value);
            DaprLogicalPrefixClaim prefix = value.Prefix;
            if (!value.SelectionHash.Span.SequenceEqual(selectionHash) || value.CoveredSequence != selection.CoveredSequence || !value.InitialStateHash.Span.SequenceEqual(selection.CanonicalStateHash.Span) || prefix.TenantId != selection.TenantId || prefix.Domain != selection.Domain || prefix.AggregateId != selection.AggregateId || prefix.AggregateType != selection.AggregateType || prefix.ActorHead != selection.ActorHead || prefix.TargetSequence != selection.TargetSequence || !prefix.SourceBindingHash.Span.SequenceEqual(selection.SourceBindingHash.Span) || !prefix.RegistryFingerprint.Span.SequenceEqual(selection.RegistryFingerprint.Span))
            {
                throw new InvalidOperationException("AnchorCapabilityHold: exact private anchor selection changed.");
            }

            RequireCurrent(_routes, token);
            captured = new DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim>(value.Prefix, image, decoding);
            return captured;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(image);
            decoding.Dispose();
            throw;
        }
        finally
        {
            try
            {
                token.ThrowIfCancellationRequested();
            }
            catch
            {
                captured?.Dispose();
                throw;
            }
        }
    }

    private void RequireScope(DaprLogicalAnchoredPrefixClaim value)
    {
        if (value.Prefix.Domain != _routes.Domain || !value.Prefix.RegistryFingerprint.Span.SequenceEqual(_routes.RegistryFingerprint.Span))
        {
            throw new InvalidOperationException("AnchorCapabilityHold: anchored domain or registry changed.");
        }
    }

    private byte[] SignatureHash(ReadOnlySpan<byte> claim)
    {
        using var w = new EventEvolutionBinaryWriter(checked(claim.Length + 2048));
        w.WriteRaw("HX-EV-SIG-1\0"u8);
        w.WriteByte(1);
        w.WriteByte(3);
        w.WriteByte(1);
        w.WriteString(_routes.CurrentKeyId);
        w.WriteByte(2);
        w.WriteBytes(claim);
        return w.ComputeSha256();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _publicKey.Dispose();
        CryptographicOperations.ZeroMemory(_spki);
    }
}
