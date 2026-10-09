using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Separately pins a current model/domain/key/SPKI/registry/validity tuple for logical route and prefix claims.</summary>
/// <remarks>Explicit local construction qualifies local tests only; this class issues no key or production capability.</remarks>
internal sealed class DaprLogicalClaimTrust : IDisposable
{
    private readonly byte[] _spki;
    private readonly byte[] _registryFingerprint;
    private readonly ECDsa _publicKey;
    private readonly DateTimeOffset _validFrom;
    private readonly DateTimeOffset _validTo;
    private readonly TimeProvider _time;
    private readonly EventEvolutionCapabilityLoss _capabilityLoss;
    private bool _disposed;

    /// <summary>Retains one explicit logical-model trust tuple after exact SPKI and P-256 validation.</summary>
    internal DaprLogicalClaimTrust(string domain, string modelId, string currentKeyId, ReadOnlySpan<byte> spki,
        ReadOnlySpan<byte> registryFingerprint, DateTimeOffset validFrom, DateTimeOffset validTo,
        EventEvolutionCapabilityLoss capabilityLoss, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        if (modelId != DaprLogicalSourceBinding.ModelId || registryFingerprint.Length != 32
            || spki.Length is < 1 or > 1024 || validFrom >= validTo) { throw new ArgumentException("Invalid model-scoped current trust configuration."); }
        DaprLogicalClaimRules.RequireCanonicalIdentity(currentKeyId);
        _capabilityLoss = capabilityLoss ?? throw new ArgumentNullException(nameof(capabilityLoss));
        Domain = domain; CurrentKeyId = currentKeyId; _validFrom = validFrom; _validTo = validTo;
        _time = timeProvider ?? TimeProvider.System; _spki = spki.ToArray(); _registryFingerprint = registryFingerprint.ToArray();
        _publicKey = ECDsa.Create();
        try
        {
            _publicKey.ImportSubjectPublicKeyInfo(_spki, out int read);
            if (read != _spki.Length || _publicKey.KeySize != 256
                || _publicKey.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7"
                || !_publicKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(_spki)) { throw new ArgumentException("Logical keys require exact canonical P-256 SPKI."); }
        }
        catch { _publicKey.Dispose(); CryptographicOperations.ZeroMemory(_spki); CryptographicOperations.ZeroMemory(_registryFingerprint); throw; }
    }

    /// <summary>Gets the exact admitted domain.</summary>
    internal string Domain { get; }

    /// <summary>Gets the current key ID in this separate logical trust configuration.</summary>
    internal string CurrentKeyId { get; }

    /// <summary>Gets the exact pinned active registry fingerprint.</summary>
    internal ReadOnlyMemory<byte> RegistryFingerprint => _registryFingerprint;

    /// <summary>Gets this trust tuple's exact shared observed-loss scope.</summary>
    internal EventEvolutionCapabilityLoss CapabilityLoss => _capabilityLoss;

    /// <summary>Checks current key validity and observed capability before each use.</summary>
    internal void RequireCurrent(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_disposed, this);
        _capabilityLoss.RequireNoObservedLoss();
        DateTimeOffset now = _time.GetUtcNow();
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _capabilityLoss.RequireNoObservedLoss();
        if (now < _validFrom || now >= _validTo) { throw new InvalidOperationException("LogicalTrustUnavailable: current key is outside its validity interval."); }
    }

    /// <summary>Signs only a strictly decoded, current model/domain/fingerprint claim with its exact pinned private key.</summary>
    internal DaprLogicalSignedClaim Sign(byte purpose, ReadOnlySpan<byte> claim, ECDsa key,
        EventBufferBudget budget, CancellationToken cancellationToken)
    {
        RequireCurrent(cancellationToken); ArgumentNullException.ThrowIfNull(key); ArgumentNullException.ThrowIfNull(budget);
        if (claim.Length > DaprLogicalClaimCodec.MaximumClaimBytes) { throw new InvalidOperationException("ProofLimit: logical claim exceeds its ceiling."); }
        using EventBufferReservation working = budget.Reserve(checked(claim.Length * 4 + 4096));
        EventBufferReservation retained = budget.Reserve(checked(claim.Length + 64 + 256));
        byte[] privateClaim = claim.ToArray(); byte[]? signature = null;
        try
        {
            ValidateScope(purpose, privateClaim, cancellationToken);
            byte[] actualSpki = key.ExportSubjectPublicKeyInfo();
            try
            {
                if (!actualSpki.AsSpan().SequenceEqual(_spki)) { throw new InvalidOperationException("LogicalTrustUnavailable: signing key differs from the current exact SPKI."); }
            }
            finally { CryptographicOperations.ZeroMemory(actualSpki); }
            using EventEvolutionBinaryWriter input = SignatureInput(purpose, CurrentKeyId, privateClaim);
            byte[] digest = input.ComputeSha256();
            try { signature = key.SignHash(digest, DSASignatureFormat.IeeeP1363FixedFieldConcatenation); }
            finally { CryptographicOperations.ZeroMemory(digest); }
            if (signature.Length != 64) { throw new CryptographicException("Logical signatures require fixed 64-byte P1363."); }
            RequireCurrent(cancellationToken);
            if (purpose == 7) { ValidateScope(purpose, privateClaim, cancellationToken); }
            return new DaprLogicalSignedClaim(privateClaim, CurrentKeyId, signature, retained);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(privateClaim); if (signature is not null) { CryptographicOperations.ZeroMemory(signature); }
            retained.Dispose(); throw;
        }
    }

    /// <summary>Verifies current logical route signature, domain, model and registry; addressed source matching remains required.</summary>
    internal DaprLogicalVerifiedClaim<DaprLogicalRouteClaim> VerifyRoute(ReadOnlySpan<byte> claim, string keyId, ReadOnlySpan<byte> signature,
        EventBufferBudget budget, CancellationToken cancellationToken)
    {
        RequireCurrent(cancellationToken);
        EventBufferReservation decoding = budget.Reserve(checked(Math.Min(claim.Length, DaprLogicalClaimCodec.MaximumClaimBytes) * 4 + 4096));
        byte[]? privateClaim = null;
        try
        {
            privateClaim = Verify(1, claim, keyId, signature, budget, cancellationToken); DaprLogicalRouteClaim result = DaprLogicalClaimCodec.DecodeRoute(privateClaim);
            RequireCurrent(cancellationToken); return new DaprLogicalVerifiedClaim<DaprLogicalRouteClaim>(result, privateClaim, decoding);
        }
        catch { if (privateClaim is not null) { CryptographicOperations.ZeroMemory(privateClaim); } decoding.Dispose(); throw; }
    }

    /// <summary>Verifies current logical prefix signature and scope without admitting caller prior progress.</summary>
    internal DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> VerifyPrefix(ReadOnlySpan<byte> claim, string keyId, ReadOnlySpan<byte> signature,
        EventBufferBudget budget, CancellationToken cancellationToken)
    {
        RequireCurrent(cancellationToken);
        EventBufferReservation decoding = budget.Reserve(checked(Math.Min(claim.Length, DaprLogicalClaimCodec.MaximumClaimBytes) * 4 + 4096));
        byte[]? privateClaim = null;
        try
        {
            privateClaim = Verify(3, claim, keyId, signature, budget, cancellationToken); DaprLogicalPrefixClaim result = DaprLogicalClaimCodec.DecodePrefix(privateClaim);
            RequireCurrent(cancellationToken); return new DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim>(result, privateClaim, decoding);
        }
        catch { if (privateClaim is not null) { CryptographicOperations.ZeroMemory(privateClaim); } decoding.Dispose(); throw; }
    }

    /// <summary>Gets a bounded current logical command-proof validity interval, without issuing production authority.</summary>
    internal (DateTimeOffset Issued, DateTimeOffset Expires) CommandValidity(CancellationToken token)
    {
        RequireCurrent(token); DateTimeOffset issued = _time.GetUtcNow().ToUniversalTime(); token.ThrowIfCancellationRequested();
        DateTimeOffset expires = issued > DateTimeOffset.MaxValue - TimeSpan.FromMinutes(5) ? _validTo : issued.AddMinutes(5);
        if (expires > _validTo) { expires = _validTo; }
        RequireCurrent(token);
        return (issued, expires);
    }

    /// <summary>Checks current logical command validity, exact domain/model registry and original cancellation.</summary>
    internal void RequireCommandCurrent(DaprLogicalCommandStateClaim claim, CancellationToken token)
    {
        RequireCurrent(token); DateTimeOffset now = _time.GetUtcNow(); token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this); _capabilityLoss.RequireNoObservedLoss();
        if (claim.Domain != Domain || !claim.RegistryFingerprint.Span.SequenceEqual(_registryFingerprint)
            || claim.IssuedAt < _validFrom || claim.ExpiresAt > _validTo || now < claim.IssuedAt || now >= claim.ExpiresAt)
        { throw new InvalidOperationException("LogicalTrustUnavailable: completed command-state scope or validity changed."); }
    }

    /// <summary>Verifies purpose 07 only for the distinct logical completed-state separator and current trust.</summary>
    internal DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> VerifyCommandState(ReadOnlySpan<byte> claim, string keyId,
        ReadOnlySpan<byte> signature, EventBufferBudget budget, CancellationToken token)
    {
        RequireCurrent(token);
        EventBufferReservation decoding = budget.Reserve(checked(Math.Min(claim.Length, DaprLogicalClaimCodec.MaximumClaimBytes) * 4 + 4096));
        byte[]? image = null;
        try
        {
            image = Verify(7, claim, keyId, signature, budget, token);
            DaprLogicalCommandStateClaim value = DaprLogicalCommandStateCodec.Decode(image);
            RequireCommandCurrent(value, token);
            return new DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim>(value, image, decoding);
        }
        catch { if (image is not null) { CryptographicOperations.ZeroMemory(image); } decoding.Dispose(); throw; }
    }

    private byte[] Verify(byte purpose, ReadOnlySpan<byte> claim, string keyId, ReadOnlySpan<byte> signature,
        EventBufferBudget budget, CancellationToken cancellationToken)
    {
        RequireCurrent(cancellationToken); ArgumentNullException.ThrowIfNull(budget);
        if (claim.Length > DaprLogicalClaimCodec.MaximumClaimBytes || signature.Length != 64 || keyId != CurrentKeyId) { throw new InvalidOperationException("LogicalTrustUnavailable: claim size, signature or current key mismatch."); }
        using EventBufferReservation working = budget.Reserve(checked(claim.Length * 4 + 4096));
        byte[] privateClaim = claim.ToArray();
        try
        {
            using EventEvolutionBinaryWriter input = SignatureInput(purpose, keyId, privateClaim);
            byte[] digest = input.ComputeSha256();
            bool verified;
            try { verified = _publicKey.VerifyHash(digest, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation); }
            finally { CryptographicOperations.ZeroMemory(digest); }
            if (!verified) { throw new InvalidOperationException("LogicalTrustUnavailable: invalid logical signature."); }
            ValidateScope(purpose, privateClaim, cancellationToken); RequireCurrent(cancellationToken); return privateClaim;
        }
        catch { CryptographicOperations.ZeroMemory(privateClaim); throw; }
    }

    private void ValidateScope(byte purpose, ReadOnlySpan<byte> claim, CancellationToken token)
    {
        string domain; ReadOnlyMemory<byte> registry;
        if (purpose == 1) { DaprLogicalRouteClaim route = DaprLogicalClaimCodec.DecodeRoute(claim); domain = route.Domain; registry = route.RegistryFingerprint; }
        else if (purpose == 3) { DaprLogicalPrefixClaim prefix = DaprLogicalClaimCodec.DecodePrefix(claim); domain = prefix.Domain; registry = prefix.RegistryFingerprint; }
        else if (purpose == 7) { DaprLogicalCommandStateClaim state = DaprLogicalCommandStateCodec.Decode(claim); RequireCommandCurrent(state, token); domain = state.Domain; registry = state.RegistryFingerprint; }
        else { throw new ArgumentException("Only separately identified logical route, prefix and command-state purposes are admitted."); }
        if (domain != Domain || !CryptographicOperations.FixedTimeEquals(registry.Span, _registryFingerprint)) { throw new InvalidOperationException("LogicalTrustUnavailable: domain or current registry fingerprint mismatch."); }
    }

    private static EventEvolutionBinaryWriter SignatureInput(byte purpose, string keyId, ReadOnlySpan<byte> claim)
    {
        var writer = new EventEvolutionBinaryWriter(checked(claim.Length + 2048));
        try
        {
            writer.WriteRaw("HX-EV-SIG-1\0"u8); writer.WriteByte(1); writer.WriteByte(purpose); writer.WriteByte(1);
            writer.WriteString(keyId); writer.WriteByte(2); writer.WriteBytes(claim); return writer;
        }
        catch { writer.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true; _publicKey.Dispose();
        CryptographicOperations.ZeroMemory(_spki); CryptographicOperations.ZeroMemory(_registryFingerprint);
    }
}
