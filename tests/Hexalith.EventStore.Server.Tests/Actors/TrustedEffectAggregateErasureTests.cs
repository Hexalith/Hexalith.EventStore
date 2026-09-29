using System.Runtime.Serialization;

using Hexalith.EventStore.Server.Actors;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Dapr remoting serialization of lifecycle-issued fence and erasure requests.</summary>
public sealed class TrustedEffectAggregateErasureTests
{
    /// <summary>
    /// Dapr actor remoting uses <see cref="DataContractSerializer"/>; every member, including the
    /// non-positional <see cref="TrustedEffectAggregateErasure.Purpose"/>, must survive a round trip.
    /// </summary>
    [Fact]
    public void DataContractSerializationRoundTripPreservesEveryMember()
    {
        var approvedAt = new DateTimeOffset(2025, 8, 21, 9, 30, 0, TimeSpan.Zero);
        var original = new TrustedEffectAggregateErasure(
            "tenant-a",
            "works",
            "target-1",
            ["EFFECT-A", "EFFECT-B"],
            "INVENTORY-DIGEST",
            approvedAt,
            approvedAt.AddDays(401),
            approvedAt.AddDays(401).AddMinutes(5),
            new string('C', 32),
            "v1." + new string('D', 64))
        {
            Purpose = TrustedEffectAggregateErasure.DeletionFencePurpose,
        };

        var serializer = new DataContractSerializer(typeof(TrustedEffectAggregateErasure));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, original);
        stream.Position = 0;
        var copy = (TrustedEffectAggregateErasure)serializer.ReadObject(stream)!;

        copy.Tenant.ShouldBe(original.Tenant);
        copy.Domain.ShouldBe(original.Domain);
        copy.Aggregate.ShouldBe(original.Aggregate);
        copy.EffectIds.ShouldBe(original.EffectIds);
        copy.InventoryDigest.ShouldBe(original.InventoryDigest);
        copy.DeletionApprovedAt.ShouldBe(original.DeletionApprovedAt);
        copy.IssuedAt.ShouldBe(original.IssuedAt);
        copy.ExpiresAt.ShouldBe(original.ExpiresAt);
        copy.Nonce.ShouldBe(original.Nonce);
        copy.Capability.ShouldBe(original.Capability);
        copy.Purpose.ShouldBe(TrustedEffectAggregateErasure.DeletionFencePurpose);
    }
}
