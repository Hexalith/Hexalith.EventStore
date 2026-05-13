using Hexalith.EventStore.Contracts.Problems;

namespace Hexalith.EventStore.Contracts.Tests.Problems;

public class GatewayProblemDetailsExtensionsTests {
    [Fact]
    public void Constants_MatchPublicGatewayExtensionNames() {
        GatewayProblemDetailsExtensions.CorrelationId.ShouldBe("correlationId");
        GatewayProblemDetailsExtensions.TenantId.ShouldBe("tenantId");
        GatewayProblemDetailsExtensions.Errors.ShouldBe("errors");
        GatewayProblemDetailsExtensions.Reason.ShouldBe("reason");
        GatewayProblemDetailsExtensions.RetryAfter.ShouldBe("retryAfter");
    }
}
