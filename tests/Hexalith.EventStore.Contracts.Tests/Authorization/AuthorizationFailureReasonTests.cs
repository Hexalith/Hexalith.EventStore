using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Contracts.Tests.Authorization;

public class AuthorizationFailureReasonTests {
    [Theory]
    [InlineData(AuthorizationFailureReason.AuthenticationRequired, "authentication_required")]
    [InlineData(AuthorizationFailureReason.SubjectMissing, "subject_missing")]
    [InlineData(AuthorizationFailureReason.TenantMissing, "tenant_missing")]
    [InlineData(AuthorizationFailureReason.TenantMismatch, "tenant_mismatch")]
    [InlineData(AuthorizationFailureReason.TenantNotFound, "tenant_not_found")]
    [InlineData(AuthorizationFailureReason.TenantDisabled, "tenant_disabled")]
    [InlineData(AuthorizationFailureReason.TenantSuspended, "tenant_suspended")]
    [InlineData(AuthorizationFailureReason.TenantStale, "tenant_stale")]
    [InlineData(AuthorizationFailureReason.TenantUnavailable, "tenant_unavailable")]
    [InlineData(AuthorizationFailureReason.TenantAmbiguous, "tenant_ambiguous")]
    [InlineData(AuthorizationFailureReason.PrincipalNotMember, "principal_not_member")]
    [InlineData(AuthorizationFailureReason.InsufficientRole, "insufficient_role")]
    [InlineData(AuthorizationFailureReason.InsufficientPermission, "insufficient_permission")]
    [InlineData(AuthorizationFailureReason.AuthorizationServiceUnavailable, "authorization_service_unavailable")]
    public void ToReasonCode_ReturnsStablePublicCode(AuthorizationFailureReason reason, string expected) => reason.ToReasonCode().ShouldBe(expected);

    [Fact]
    public void FromReasonCode_UnknownValueFailsClosedAsUnavailable() => AuthorizationFailureReasonExtensions.FromReasonCode("not-a-public-code")
            .ShouldBe(AuthorizationFailureReason.AuthorizationServiceUnavailable);
}
