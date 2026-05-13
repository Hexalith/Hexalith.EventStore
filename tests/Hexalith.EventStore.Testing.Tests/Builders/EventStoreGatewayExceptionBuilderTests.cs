using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Testing.Builders;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests.Builders;

public class EventStoreGatewayExceptionBuilderTests {
    [Fact]
    public void Validation_CreatesProblemDetailsExceptionWithErrors() {
        EventStoreGatewayException exception = EventStoreGatewayExceptionBuilder
            .Validation("corr-1", "tenant-a", new Dictionary<string, string> { ["tenant"] = "Tenant is required." })
            .Build();

        exception.StatusCode.ShouldBe(400);
        exception.Title.ShouldBe("Validation Failed");
        exception.CorrelationId.ShouldBe("corr-1");
        exception.TenantId.ShouldBe("tenant-a");
        exception.Errors["tenant"].ShouldBe("Tenant is required.");
    }

    [Fact]
    public void StandardFactories_CoverGatewayFailureCategories() {
        EventStoreGatewayExceptionBuilder.AuthenticationRequired("corr-auth").Build().StatusCode.ShouldBe(401);
        EventStoreGatewayExceptionBuilder.AuthorizationDenied("corr-denied", "tenant-a").Build().StatusCode.ShouldBe(403);
        EventStoreGatewayExceptionBuilder.Conflict("corr-conflict", "tenant-a").Build().StatusCode.ShouldBe(409);
        EventStoreGatewayExceptionBuilder.Stale("corr-stale", "tenant-a").Build().StatusCode.ShouldBe(409);
        EventStoreGatewayExceptionBuilder.Unavailable("corr-down", "tenant-a", retryAfter: "PT30S").Build().StatusCode.ShouldBe(503);
        EventStoreGatewayExceptionBuilder.Unexpected("corr-oops").Build().StatusCode.ShouldBe(500);
    }
}
