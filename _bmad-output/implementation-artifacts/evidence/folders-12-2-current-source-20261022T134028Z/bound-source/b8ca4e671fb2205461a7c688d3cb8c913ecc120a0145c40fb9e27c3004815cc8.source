using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed class NamedProjectionRetryFixtureTests
{
    [Fact]
    public void UnrelatedTerminalItemDoesNotBorrowOriginalStreamBindings()
    {
        var identity = new AggregateIdentity("tenant-a", "counter", "original-aggregate");
        var proof = NativeSourceProofCodec.ExtendDigest(
            NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            identity, 1, "original-message", new('a', 64));
        var original = new ProjectionDeliveryRetryWorkItem(
            "original-work", identity.TenantId, identity.Domain, identity.AggregateId,
            "fixture-app", "v1", 1, "original-message", ["counter-index"], [],
            "original-message", new('b', 64), 0, DateTimeOffset.UtcNow, null)
        {
            NativeSourceProof = proof,
            ReplayRecoveryBindings = new Dictionary<string, string> { ["counter-index"] = new('c', 64) },
        };

        ProjectionDeliveryRetryWorkItem unrelated =
            NamedProjectionDispatchLiveSidecarTests.CreateUnrelatedSameShardWorkItem(original);

        unrelated.AggregateId.ShouldNotBe(original.AggregateId);
        unrelated.HeadMessageId.ShouldNotBe(original.HeadMessageId);
        unrelated.NativeSourceProof.ShouldBeNull();
        unrelated.ReplayRecoveryBindings.ShouldBeNull();
        original.NativeSourceProof.ShouldBe(proof);
        original.ReplayRecoveryBindings["counter-index"].ShouldBe(new string('c', 64));
        (SHA256.HashData(Encoding.UTF8.GetBytes(unrelated.WorkId))[0] % 64)
            .ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(original.WorkId))[0] % 64);
    }
}
