using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public class VerifiedEffectiveEventViewTests {
    [Fact]
    public void UsesApprovedWebPropertyNamesAndCopiesByteArrays() {
        var view = new VerifiedEffectiveEventView(
            7,
            new byte[32],
            "counter-incremented",
            2,
            "json",
            [1, 2],
            [3],
            "route-key",
            new byte[64]);

        string json = JsonSerializer.Serialize(view, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.ShouldContain("\"sequenceNumber\":7");
        json.ShouldContain("\"storedDigest\"");
        json.ShouldContain("\"eventContractType\":\"counter-incremented\"");
        json.ShouldContain("\"payloadVersion\":2");
        json.ShouldContain("\"serializationFormat\":\"json\"");
        json.ShouldContain("\"effectivePayload\"");
        json.ShouldContain("\"routeClaim\"");
        json.ShouldContain("\"routeKeyId\":\"route-key\"");
        json.ShouldContain("\"routeSignature\"");

        byte[] copy = view.EffectivePayload;
        copy[0] = 9;
        view.EffectivePayload.ShouldBe([1, 2]);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsOversizedFixedProofBeforeAllocatingTransportCopies(bool signature) {
        byte[] oversized = new byte[4 * 1024 * 1024];
        long before = GC.GetAllocatedBytesForCurrentThread();
        Should.Throw<ArgumentException>(() => new VerifiedEffectiveEventView(
            1, signature ? new byte[32] : oversized, "counter-incremented", 2, "json",
            [], [], "route-key", signature ? oversized : new byte[64]));
        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBeLessThan(1024 * 1024);
    }

    [Fact]
    public void RejectsOverlargeRouteClaimBeforeCopyingPayload() {
        byte[] payload = new byte[2 * 1024 * 1024];
        byte[] claim = new byte[1024 * 1024 + 1];
        long before = GC.GetAllocatedBytesForCurrentThread();
        Should.Throw<ArgumentOutOfRangeException>(() => new VerifiedEffectiveEventView(
            1, new byte[32], "counter-incremented", 2, "json", payload, claim, "route-key", new byte[64]));
        (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBeLessThan(1024 * 1024);
    }

    [Fact]
    public void AllowsTransportPayloadAboveHopCeilingForUnverifiedZeroHopV1Carrier() {
        var view = new VerifiedEffectiveEventView(
            1, new byte[32], "counter-incremented", 2, "json", new byte[1024 * 1024 + 1],
            [1], "route-key", new byte[64]);
        view.EffectivePayload.Length.ShouldBe(1024 * 1024 + 1);
    }

}
