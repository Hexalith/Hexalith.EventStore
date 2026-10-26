using System.Reflection;

using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

public class EventStoreOperationCompatibilityTests
{
    [Theory]
    [InlineData("DomainServiceProcess", "domain-service:process")]
    [InlineData("DomainServiceReplayState", "domain-service:replay-state")]
    [InlineData("DomainServiceQuery", "domain-service:query")]
    [InlineData("DomainServiceProject", "domain-service:project")]
    [InlineData("DomainServiceMetadata", "domain-service:metadata")]
    [InlineData("ProjectionNotify", "projection:notify")]
    [InlineData("TrustedEffect", "eventstore:trusted-effect")]
    [InlineData("GatewayCommandSubmit", "eventstore:command-submit")]
    [InlineData("GatewayCommandStatus", "eventstore:command-status")]
    [InlineData("GatewayStreamRead", "eventstore:stream-read")]
    [InlineData("GatewayOwnerAuthorityQuery", "eventstore:owner-authority-query")]
    public void ExistingPublicConstantsRemainLiteralAndCompatible(string name, string value)
    {
        FieldInfo old = typeof(EventStoreWorkloadOperations).GetField(name)!;
        FieldInfo shared = typeof(EventStoreOperationIdentifiers).GetField(name)!;
        old.IsLiteral.ShouldBeTrue();
        shared.IsLiteral.ShouldBeTrue();
        old.GetRawConstantValue().ShouldBe(value);
        shared.GetRawConstantValue().ShouldBe(value);
    }

    [Fact]
    public void HumanPermissionClaimRemainsSeparateFromWorkloadOperation()
    {
        JwtBearerAuthenticationContract.HumanPermissionClaimType.ShouldBe("eventstore:permission");
        JwtBearerAuthenticationContract.HumanPermissionClaimType.ShouldNotBe("eventstore:operation");
    }
}
