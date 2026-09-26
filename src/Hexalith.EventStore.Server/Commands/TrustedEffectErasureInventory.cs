using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Canonicalizes the complete tenant inventory for lifecycle-issued erasure capabilities.</summary>
public static class TrustedEffectErasureInventory
{
    /// <summary>Creates one unsigned request per source or target aggregate partition.</summary>
    public static TrustedEffectAggregateErasure[] Build(
        string tenant,
        EffectIdentity[] inventory,
        DateTimeOffset deletionApprovedAt,
        DateTimeOffset issuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Length == 0 || inventory.Any(identity => identity is null
            || !string.Equals(identity.Tenant, tenant, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Trusted effect erasure inventory is invalid.");
        }

        string digest = ComputeDigest(inventory);
        AggregateIdentity[] partitions = inventory
            .SelectMany(static identity => new[]
            {
                new AggregateIdentity(identity.Tenant, identity.SourceDomain, identity.SourceAggregate),
                new AggregateIdentity(identity.Tenant, identity.TargetDomain, identity.TargetAggregate),
            })
            .DistinctBy(static identity => identity.ActorId)
            .OrderBy(static identity => identity.ActorId, StringComparer.Ordinal)
            .ToArray();
        return partitions.Select(partition => new TrustedEffectAggregateErasure(
            tenant,
            partition.Domain,
            partition.AggregateId,
            inventory.Where(identity =>
                    string.Equals(identity.TargetDomain, partition.Domain, StringComparison.Ordinal)
                    && string.Equals(identity.TargetAggregate, partition.AggregateId, StringComparison.Ordinal))
                .Select(EffectIdentityCodec.ComputeEffectId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            digest,
            deletionApprovedAt,
            issuedAt,
            issuedAt.AddMinutes(5),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            string.Empty)).ToArray();
    }

    /// <summary>Computes a collision-resistant digest of the sorted complete identity tuples.</summary>
    public static string ComputeDigest(EffectIdentity[] inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Length == 0)
        {
            throw new InvalidOperationException("Trusted effect erasure inventory is empty.");
        }

        byte[][] tuples = inventory.Select(EffectIdentityCodec.Encode)
            .OrderBy(static tuple => Convert.ToHexString(tuple), StringComparer.Ordinal)
            .ToArray();
        var output = new ArrayBufferWriter<byte>();
        foreach (byte[] tuple in tuples)
        {
            Span<byte> target = output.GetSpan(sizeof(int) + tuple.Length);
            BinaryPrimitives.WriteInt32BigEndian(target, tuple.Length);
            tuple.CopyTo(target[sizeof(int)..]);
            output.Advance(sizeof(int) + tuple.Length);
        }

        return EffectIdentityCodec.RenderDigest(SHA256.HashData(output.WrittenSpan));
    }
}
