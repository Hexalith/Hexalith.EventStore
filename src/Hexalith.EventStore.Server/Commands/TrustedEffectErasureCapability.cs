using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Signs lifecycle purge decisions for exact actor partitions and inventories.</summary>
public sealed class TrustedEffectErasureCapability(
    IIdempotencyDigestKeyProvider keyProvider,
    TimeProvider? timeProvider = null) : ITrustedEffectErasureAuthority
{
    private static readonly byte[] _domain = "hexalith-trusted-effect-erasure-v1\0"u8.ToArray();
    private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public async Task<string> IssueAsync(
        TrustedEffectAggregateErasure request,
        CancellationToken cancellationToken = default)
    {
        ValidateShape(request, Clock.GetUtcNow());
        if (!string.IsNullOrEmpty(request.Capability))
        {
            throw new InvalidOperationException("Erasure capability request is already signed.");
        }

        using IdempotencyDigestKeyRing ring = await keyProvider.GetKeyRingAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] key = ring.RentKeyMaterial(ring.ActiveVersion);
        try
        {
            return ring.ActiveVersion + "." + Convert.ToHexString(HMACSHA256.HashData(key, Encode(request)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc/>
    public async Task ValidateAsync(
        TrustedEffectAggregateErasure request,
        CancellationToken cancellationToken = default)
    {
        ValidateShape(request, Clock.GetUtcNow());
        int separator = request.Capability.IndexOf('.');
        if (separator <= 0 || request.Capability.Length - separator - 1 != 64)
        {
            throw new InvalidOperationException("Trusted effect erasure capability is invalid.");
        }

        string version = request.Capability[..separator];
        using IdempotencyDigestKeyRing ring = await keyProvider.GetKeyRingAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!ring.Versions.Contains(version, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Trusted effect erasure capability key is unavailable.");
        }

        byte[] key = ring.RentKeyMaterial(version);
        try
        {
            byte[] expected = HMACSHA256.HashData(key, Encode(request));
            byte[] actual;
            try
            {
                actual = Convert.FromHexString(request.Capability[(separator + 1)..]);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Trusted effect erasure capability is invalid.");
            }

            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                throw new InvalidOperationException("Trusted effect erasure capability does not match the purge decision.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void ValidateShape(TrustedEffectAggregateErasure request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Tenant)
            || string.IsNullOrWhiteSpace(request.Domain)
            || string.IsNullOrWhiteSpace(request.Aggregate)
            || string.IsNullOrWhiteSpace(request.InventoryDigest)
            || request.Nonce is null
            || request.Nonce.Length != 32
            || request.Capability is null
            || request.EffectIds is null
            || request.EffectIds.Any(string.IsNullOrWhiteSpace)
            || request.EffectIds.Distinct(StringComparer.Ordinal).Count() != request.EffectIds.Length
            || request.DeletionApprovedAt > request.IssuedAt
            || request.IssuedAt > now.AddSeconds(30)
            || request.ExpiresAt <= now
            || request.ExpiresAt - request.IssuedAt != TimeSpan.FromMinutes(5))
        {
            throw new InvalidOperationException("Trusted effect erasure capability scope or lifetime is invalid.");
        }
    }

    private static byte[] Encode(TrustedEffectAggregateErasure request)
    {
        var output = new ArrayBufferWriter<byte>();
        Write(output, _domain);
        Write(output, Encoding.UTF8.GetBytes("PurgeEligible"));
        Write(output, Encoding.UTF8.GetBytes(request.Tenant));
        Write(output, Encoding.UTF8.GetBytes(request.Domain));
        Write(output, Encoding.UTF8.GetBytes(request.Aggregate));
        Write(output, Encoding.UTF8.GetBytes(request.InventoryDigest));
        Write(output, Encoding.UTF8.GetBytes(request.Nonce));
        WriteLong(output, request.EffectIds.Length);
        foreach (string effectId in request.EffectIds.Order(StringComparer.Ordinal))
        {
            Write(output, Encoding.UTF8.GetBytes(effectId));
        }

        WriteLong(output, request.DeletionApprovedAt.ToUniversalTime().Ticks);
        WriteLong(output, request.IssuedAt.ToUniversalTime().Ticks);
        WriteLong(output, request.ExpiresAt.ToUniversalTime().Ticks);
        return output.WrittenSpan.ToArray();
    }

    private static void Write(ArrayBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        Span<byte> target = output.GetSpan(sizeof(int) + value.Length);
        BinaryPrimitives.WriteInt32BigEndian(target, value.Length);
        value.CopyTo(target[sizeof(int)..]);
        output.Advance(sizeof(int) + value.Length);
    }

    private static void WriteLong(ArrayBufferWriter<byte> output, long value)
    {
        Span<byte> target = output.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(target, value);
        output.Advance(sizeof(long));
    }
}
