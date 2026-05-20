using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Authorization;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class AuthorizationResultReasonCodeTests {
    [Fact]
    public void TenantDenied_CarriesReasonCodeSeparatelyFromReasonText() {
        var result = TenantValidationResult.Denied(
            "Tenant is disabled.",
            AuthorizationFailureReason.TenantDisabled);

        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Tenant is disabled.");
        result.ReasonCode.ShouldBe(AuthorizationFailureReason.TenantDisabled);
        result.ReasonCode.ToReasonCode().ShouldBe("tenant_disabled");
    }

    [Fact]
    public void RbacDenied_CarriesReasonCodeSeparatelyFromReasonText() {
        var result = RbacValidationResult.Denied(
            "Permission is missing.",
            AuthorizationFailureReason.InsufficientPermission);

        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Permission is missing.");
        result.ReasonCode.ShouldBe(AuthorizationFailureReason.InsufficientPermission);
        result.ReasonCode.ToReasonCode().ShouldBe("insufficient_permission");
    }
}
