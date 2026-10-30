using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public class RouteCatalogSurfaceTests
{
    [Fact]
    public void CompleteCanonicalPayloadBindsEveryMemberAndValueWithoutObjectOrderDrift()
    {
        ReadOnlyMemory<byte> first = "{\"amount\":1,\"optional\":null,\"extra\":{\"a\":1,\"b\":2}}"u8.ToArray();
        ReadOnlyMemory<byte> reordered = "{\"extra\":{\"b\":2,\"a\":1},\"optional\":null,\"amount\":1}"u8.ToArray();
        var hash = DomainService.CanonicalIntentPayload.Sha256CanonicalJson(first);
        DomainService.CanonicalIntentPayload.Sha256CanonicalJson(reordered).ShouldBe(hash);
        DomainService.CanonicalIntentPayload.Sha256CanonicalJson("{\"amount\":1,\"optional\":null,\"extra\":{\"a\":1,\"b\":3}}"u8.ToArray()).ShouldNotBe(hash);
        DomainService.CanonicalIntentPayload.Sha256CanonicalJson("{\"amount\":1,\"extra\":{\"a\":1,\"b\":2}}"u8.ToArray()).ShouldNotBe(hash);
    }
    [Theory]
    [InlineData("ui", "agent-eligible", false, true)]
    [InlineData("ui", "UI-only", false, true)]
    [InlineData("ui", "confirmation-required", false, true)]
    [InlineData("agent", "agent-eligible", false, true)]
    [InlineData("agent", "UI-only", false, false)]
    [InlineData("agent", "confirmation-required", true, false)]
    [InlineData("service", "agent-eligible", false, true)]
    [InlineData("service", "UI-only", true, false)]
    [InlineData("service", "confirmation-required", false, false)]
    [InlineData("service", "confirmation-required", true, true)]
    [InlineData("ui", "step-up", true, false)]
    [InlineData("agent", "step-up", true, false)]
    [InlineData("service", "step-up", true, false)]
    public void ExplicitSurfaceAndEligibilityGovernAdmission(string surface, string eligibility, bool proof, bool allowed)
        => RouteCatalogSurfacePolicy.Allows(Operation(eligibility), Principal(surface), proof).ShouldBe(allowed);

    [Fact]
    public void MissingAmbiguousUnauthenticatedOrInvalidSurfaceNeverAdmits()
    {
        foreach (var user in new[] { Principal(), Principal("ui", "agent"), Principal("ui", "ui"), Principal("admin"),
            new ClaimsPrincipal(new ClaimsIdentity([new(RouteCatalogSurfacePolicy.ClaimType, "ui")])) })
            RouteCatalogSurfacePolicy.Allows(Operation("agent-eligible"), user).ShouldBeFalse();
    }

    [Theory]
    [InlineData("ui")]
    [InlineData("agent")]
    [InlineData("service")]
    public void PublicSurfacesCannotPromoteAnInternalOperation(string surface)
        => RouteCatalogSurfacePolicy.Allows(Operation("agent-eligible") with { Ingress = "internal" }, Principal(surface), true).ShouldBeFalse();

    private static ClaimsPrincipal Principal(params string[] surfaces) => new(new ClaimsIdentity(
        surfaces.Select(surface => new Claim(RouteCatalogSurfacePolicy.ClaimType, surface)), "validated-jwt"));

    private static RouteOperationContract Operation(string eligibility) => new("test-operation", "v1", "public", eligibility,
        JsonSerializer.SerializeToElement(new { type = "object" }), JsonSerializer.SerializeToElement(new { type = "object" }), string.Empty);
}
