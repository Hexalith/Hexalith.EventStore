using System.Security.Claims;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authentication;

public class EventStoreRateLimitPartitionTests {
    [Fact]
    public void VerifiedWorkload_HasOwnPartition() {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "workload:chatbot"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "chatbot"),
            ], DaprInternalAuthenticationOptions.SchemeName));

        EventStoreRateLimitPartition.TrustedInternalKey(principal).ShouldBe("workload:chatbot");
    }

    [Fact]
    public void ClaimedWorkload_WithoutVerifiedSchemeOrMatchingSubject_StaysAnonymous() {
        var userJwt = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "workload:chatbot"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "chatbot"),
            ], "Bearer"));
        var mismatchedWorkload = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "workload:other"),
                new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "chatbot"),
            ], DaprInternalAuthenticationOptions.SchemeName));

        EventStoreRateLimitPartition.TrustedInternalKey(userJwt).ShouldBeNull();
        EventStoreRateLimitPartition.TrustedInternalKey(mismatchedWorkload).ShouldBeNull();
    }

    [Fact]
    public void OnlyVerifiedSidecar_HasOwnPartition() {
        static ClaimsPrincipal Channel(string value) => new(new ClaimsIdentity(
            [new Claim(EventStoreWorkloadAuthenticationDefaults.ChannelClaimType, value)],
            EventStoreWorkloadAuthenticationDefaults.SidecarChannelScheme));

        EventStoreRateLimitPartition.TrustedInternalKey(Channel(DaprSidecarChannelAuthenticationHandler.VerifiedChannel))
            .ShouldBe("sidecar:verified");
        EventStoreRateLimitPartition.TrustedInternalKey(Channel(DaprSidecarChannelAuthenticationHandler.DevelopmentUnverifiedChannel))
            .ShouldBeNull();
    }
}
