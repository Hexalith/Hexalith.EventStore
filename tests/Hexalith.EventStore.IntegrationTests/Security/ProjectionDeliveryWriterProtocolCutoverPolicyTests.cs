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

    [Fact]
    public void WriterProtocolIsOnlyUnhealthyCheck_HealthySiblings_ReturnsTrue() {
        const string healthBody =
            """
            {"results":{"projection-delivery-writer-protocol":{"status":"Unhealthy"},"redis":{"status":"Healthy"}}}
            """;

        ProjectionDeliveryWriterProtocolCutoverPolicy.WriterProtocolIsOnlyUnhealthyCheck(
            healthBody,
            "projection-delivery-writer-protocol").ShouldBeTrue();
    }

    [Theory]
    [InlineData("Degraded")]
    [InlineData("Unhealthy")]
    [InlineData("healthy")]
    public void WriterProtocolIsOnlyUnhealthyCheck_NonHealthySibling_ReturnsFalse(string siblingStatus) {
        const string healthTemplate =
            """
            {"results":{"projection-delivery-writer-protocol":{"status":"Unhealthy"},"redis":{"status":"__STATUS__"}}}
            """;
        string healthBody = healthTemplate.Replace("__STATUS__", siblingStatus, StringComparison.Ordinal);

        ProjectionDeliveryWriterProtocolCutoverPolicy.WriterProtocolIsOnlyUnhealthyCheck(
            healthBody,
            "projection-delivery-writer-protocol").ShouldBeFalse();
    }

    [Fact]
    public void WriterProtocolIsOnlyUnhealthyCheck_NonStringSiblingStatus_ReturnsFalse() {
        const string healthBody =
            """
            {"results":{"projection-delivery-writer-protocol":{"status":"Unhealthy"},"redis":{"status":1}}}
            """;

        ProjectionDeliveryWriterProtocolCutoverPolicy.WriterProtocolIsOnlyUnhealthyCheck(
            healthBody,
            "projection-delivery-writer-protocol").ShouldBeFalse();
    }
}
