using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class VerifiedEffectiveCommandEventTests {
    [Fact]
    public void CopiesProofAndPayloadBytesAtConstructionAndRead() {
        byte[] storedDigest = new byte[32];
        byte[] effectivePayload = [1, 2, 3];
        byte[] routeClaim = [4, 5];
        byte[] routeSignature = new byte[64];
        var eventView = new VerifiedEffectiveCommandEvent(
            1,
            storedDigest,
            "counter-incremented",
            2,
            "json",
            effectivePayload,
            routeClaim,
            "event-key",
            routeSignature);

        storedDigest[0] = 1;
        effectivePayload[0] = 9;
        routeClaim[0] = 9;
        routeSignature[0] = 1;
        byte[] returnedPayload = eventView.EffectivePayload;
        returnedPayload[1] = 9;

        eventView.StoredDigest[0].ShouldBe((byte)0);
        eventView.EffectivePayload.ShouldBe([1, 2, 3]);
        eventView.RouteClaim.ShouldBe([4, 5]);
        eventView.RouteSignature[0].ShouldBe((byte)0);
    }

    [Theory]
    [InlineData("Uppercase")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("contains_underscore")]
    public void RejectsNonCanonicalEventContractTypes(string eventContractType) {
        Should.Throw<ArgumentException>(() => new VerifiedEffectiveCommandEvent(
            1,
            new byte[32],
            eventContractType,
            1,
            "json",
            [],
            [],
            "route-key",
            new byte[64]));
    }
}
