using System.Net;

using Shouldly;

namespace Hexalith.EventStore.IntegrationTests.Security;

public class ProjectionDeliveryWriterProtocolCutoverPolicyTests {
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void IsTransientActivationStatus_RetryableResponse_ReturnsTrue(HttpStatusCode statusCode) {
        ProjectionDeliveryWriterProtocolCutoverPolicy.IsTransientActivationStatus(statusCode).ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void IsTransientActivationStatus_PermanentResponse_ReturnsFalse(HttpStatusCode statusCode) {
        ProjectionDeliveryWriterProtocolCutoverPolicy.IsTransientActivationStatus(statusCode).ShouldBeFalse();
    }
}
