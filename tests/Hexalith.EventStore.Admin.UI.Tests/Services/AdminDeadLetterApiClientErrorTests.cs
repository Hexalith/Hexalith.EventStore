using System.Net;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Admin.UI.Services.Exceptions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

public class AdminDeadLetterApiClientErrorTests {
    [Fact]
    public async Task RetryDeadLettersAsync_ProblemDetails_PreservesSafeFieldsWithoutRawBody() {
        using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) {
            Content = new StringContent(
                JsonSerializer.Serialize(new {
                    title = "Validation failed",
                    detail = "Message cannot be retried",
                    status = 422,
                    errorCode = "DLQ_INVALID_STATE",
                    traceId = "trace-123",
                    operationId = "op-456",
                    raw = "Bearer secret-token redis://internal-host:6379",
                }),
                Encoding.UTF8,
                "application/problem+json"),
        };
        AdminDeadLetterApiClient client = CreateClient(response);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.RetryDeadLettersAsync("tenant-a", ["msg-1"]));

        ex.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        ex.Title.ShouldBe("Validation failed");
        ex.Detail.ShouldBe("Message cannot be retried");
        ex.ErrorCode.ShouldBe("DLQ_INVALID_STATE");
        ex.TraceId.ShouldBe("trace-123");
        ex.OperationId.ShouldBe("op-456");
        ex.Message.ShouldNotContain("secret-token");
        ex.Message.ShouldNotContain("redis://internal-host");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Authentication required")]
    [InlineData(HttpStatusCode.Forbidden, "Access denied")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "temporarily unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "Admin API returned 500")]
    public async Task RetryDeadLettersAsync_GenericFailure_PreservesStatusCategory(HttpStatusCode statusCode, string expectedMessage) {
        using var response = new HttpResponseMessage(statusCode) {
            Content = new StringContent(string.Empty),
        };
        AdminDeadLetterApiClient client = CreateClient(response);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.RetryDeadLettersAsync("tenant-a", ["msg-1"]));

        ex.StatusCode.ShouldBe(statusCode);
        ex.Message.ShouldContain(expectedMessage);
    }

    [Fact]
    public async Task RetryDeadLettersAsync_MalformedProblemBody_UsesBoundedDiagnostic() {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent("Bearer secret-token raw service dump"),
        };
        AdminDeadLetterApiClient client = CreateClient(response);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.RetryDeadLettersAsync("tenant-a", ["msg-1"]));

        ex.Message.ShouldContain("could not be parsed");
        ex.Message.ShouldNotContain("secret-token");
        ex.Message.ShouldNotContain("raw service dump");
    }

    [Theory]
    [InlineData("\"oops\"")]
    [InlineData("[]")]
    public async Task RetryDeadLettersAsync_NonObjectProblemBody_UsesBoundedDiagnostic(string body) {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        AdminDeadLetterApiClient client = CreateClient(response);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.RetryDeadLettersAsync("tenant-a", ["msg-1"]));

        ex.Message.ShouldContain("could not be parsed");
    }

    [Fact]
    public async Task RetryDeadLettersAsync_ProblemDetailRedactsSecretsAndInternalEvidence() {
        using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) {
            Content = new StringContent(
                JsonSerializer.Serialize(new {
                    title = "Validation failed",
                    detail = "Bearer secret-token at Internal.Type.Method() server01.internal Data Source=db;User ID=sa;Password=pw",
                    errorCode = "DLQ_INVALID_STATE",
                }),
                Encoding.UTF8,
                "application/problem+json"),
        };
        AdminDeadLetterApiClient client = CreateClient(response);

        AdminApiProblemException ex = await Should.ThrowAsync<AdminApiProblemException>(
            () => client.RetryDeadLettersAsync("tenant-a", ["msg-1"]));

        _ = ex.Detail.ShouldNotBeNull();
        ex.Detail.ShouldContain("[redacted]");
        ex.Detail.ShouldNotContain("secret-token");
        ex.Detail.ShouldNotContain("server01.internal");
        ex.Detail.ShouldNotContain("Data Source=");
        ex.Detail.ShouldNotContain("Password=");
    }

    private static AdminDeadLetterApiClient CreateClient(HttpResponseMessage response) {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient("AdminApi").Returns(new HttpClient(new StubHandler(response)) {
            BaseAddress = new Uri("https://admin.test/"),
        });
        return new AdminDeadLetterApiClient(factory, NullLogger<AdminDeadLetterApiClient>.Instance);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
