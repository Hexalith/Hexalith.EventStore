using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Authenticates short-lived, exact-scope maintenance bootstrap inside the actor boundary.</summary>
internal sealed class SourceProofBootstrapAuthority(IIdempotencyDigestKeyProvider keyProvider, TimeProvider timeProvider)
{
    /// <summary>Signs only an authenticated controller's complete maintenance attestation.</summary>
    internal async Task<SourceProofBootstrapRequest> AuthorizeAsync(
        SourceProofBootstrapRequest request, CancellationToken cancellationToken)
    {
        using IdempotencyDigestKeyRing ring = await keyProvider.GetKeyRingAsync(cancellationToken).ConfigureAwait(false);
        SourceProofBootstrapRequest unsigned = request with
        {
            ExpiresAt = timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5),
            KeyVersion = ring.ActiveVersion,
            Authorization = string.Empty,
        };
        ValidateFields(unsigned);
        byte[] key = ring.RentKeyMaterial(ring.ActiveVersion);
        try
        {
            return unsigned with { Authorization = Convert.ToBase64String(Compute(key, unsigned)) };
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>Rejects a missing, expired, changed or foreign-scope maintenance authorization.</summary>
    internal async Task ValidateAsync(SourceProofBootstrapRequest request, AggregateIdentity identity)
    {
        ValidateFields(request);
        if (request.TenantId != identity.TenantId || request.Domain != identity.Domain
            || request.AggregateId != identity.AggregateId)
            throw new InvalidOperationException("guard_activation_required");
        using IdempotencyDigestKeyRing ring = await keyProvider.GetKeyRingAsync().ConfigureAwait(false);
        byte[] key = ring.RentKeyMaterial(request.KeyVersion);
        try
        {
            byte[] supplied;
            try { supplied = Convert.FromBase64String(request.Authorization); }
            catch (FormatException) { throw new InvalidOperationException("guard_activation_required"); }
            if (!CryptographicOperations.FixedTimeEquals(supplied, Compute(key, request)))
                throw new InvalidOperationException("guard_activation_required");
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private void ValidateFields(SourceProofBootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (request.ExpectedHead is < 0 or > 10_000 || !request.WritersQuiesced || !request.BackupRestoreVerified
            || string.IsNullOrWhiteSpace(request.OperatorId) || string.IsNullOrWhiteSpace(request.BackupReference)
            || request.SpecSha256 != ProjectionSourceGuard.ApprovedSpecSha256
            || request.CutoverCommit is not { Length: 40 }
            || request.CutoverCommit.Any(static value => value is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || request.ExpiresAt <= now || request.ExpiresAt > now + TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("guard_activation_required");
    }

    private static byte[] Compute(byte[] key, SourceProofBootstrapRequest request)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(request with { Authorization = string.Empty });
        byte[] material = new byte["hexalith.projection.source.bootstrap.v1\0"u8.Length + body.Length];
        "hexalith.projection.source.bootstrap.v1\0"u8.CopyTo(material);
        body.CopyTo(material, "hexalith.projection.source.bootstrap.v1\0"u8.Length);
        return HMACSHA256.HashData(key, material);
    }
}
