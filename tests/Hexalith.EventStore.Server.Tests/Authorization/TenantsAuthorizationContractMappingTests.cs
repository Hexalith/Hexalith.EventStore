using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.Tenants.Contracts.Enums;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class TenantsAuthorizationContractMappingTests {
    [Fact]
    public void TenantStatus_PublicContractValues_MapToGatewayReasonPolicy() {
        MapTenantStatus(TenantStatus.Active).ShouldBe(AuthorizationFailureReason.None);
        MapTenantStatus(TenantStatus.Disabled).ShouldBe(AuthorizationFailureReason.TenantDisabled);
    }

    [Theory]
    [InlineData(TenantRole.TenantOwner)]
    [InlineData(TenantRole.TenantContributor)]
    [InlineData(TenantRole.TenantReader)]
    public void TenantRole_PublicContractValues_AreKnownToGatewayAdapterShape(TenantRole role) => Enum.IsDefined(role).ShouldBeTrue();

    private static AuthorizationFailureReason MapTenantStatus(TenantStatus status) => status switch {
        TenantStatus.Active => AuthorizationFailureReason.None,
        TenantStatus.Disabled => AuthorizationFailureReason.TenantDisabled,
        _ => AuthorizationFailureReason.TenantAmbiguous,
    };
}
