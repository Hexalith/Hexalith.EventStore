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
}
