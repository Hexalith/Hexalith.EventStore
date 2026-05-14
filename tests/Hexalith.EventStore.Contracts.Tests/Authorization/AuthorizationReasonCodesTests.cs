using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Contracts.Tests.Authorization;

public class AuthorizationReasonCodesTests {
    [Fact]
    public void ReasonCodesShouldExposeCanonicalGatewayAuthorizationValues() {
        AuthorizationReasonCodes.AuthenticationRequired.ShouldBe("authentication_required");
        AuthorizationReasonCodes.TenantMissing.ShouldBe("tenant_missing");
        AuthorizationReasonCodes.TenantNotFound.ShouldBe("tenant_not_found");
        AuthorizationReasonCodes.TenantDisabled.ShouldBe("tenant_disabled");
        AuthorizationReasonCodes.TenantSuspended.ShouldBe("tenant_suspended");
        AuthorizationReasonCodes.TenantStale.ShouldBe("tenant_stale");
        AuthorizationReasonCodes.TenantUnavailable.ShouldBe("tenant_unavailable");
        AuthorizationReasonCodes.TenantAmbiguous.ShouldBe("tenant_ambiguous");
        AuthorizationReasonCodes.PrincipalNotMember.ShouldBe("principal_not_member");
        AuthorizationReasonCodes.InsufficientRole.ShouldBe("insufficient_role");
        AuthorizationReasonCodes.InsufficientPermission.ShouldBe("insufficient_permission");
        AuthorizationReasonCodes.AuthorizationServiceUnavailable.ShouldBe("authorization_service_unavailable");
    }

    [Fact]
    public void ReasonCodeProblemDetailsExtensionShouldBeStable() =>
        AuthorizationProblemDetailsExtensions.ReasonCode.ShouldBe("reasonCode");
}
