using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

/// <summary>
/// Test 9.12: Auth handler attaches Bearer token to outgoing requests (AC: 10).
/// Merge-blocking test.
/// </summary>
public class AdminApiAuthorizationHandlerTests {
    [Fact]
    public async Task SendAsync_AttachesBearerToken() {
        // Arrange
        Microsoft.Extensions.Configuration.IConfiguration config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Authentication:Issuer"] = "hexalith-dev",
                ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
                ["EventStore:Authentication:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars!",
                ["EventStore:Authentication:Subject"] = "test-user",
            })
            .Build();

        var tokenProvider = new AdminApiAccessTokenProvider(config);
        var handler = new AdminApiAuthorizationHandler(tokenProvider) {
            InnerHandler = new TestHandler(),
        };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        HttpResponseMessage response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_ThrowsUnauthorizedAccessException_OnUnauthorizedResponse() {
        Microsoft.Extensions.Configuration.IConfiguration config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:Authentication:Issuer"] = "hexalith-dev",
                ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
                ["EventStore:Authentication:SigningKey"] = "DevOnlySigningKey-AtLeast32Chars!",
                ["EventStore:Authentication:Subject"] = "test-user",
            })
            .Build();

        var tokenProvider = new AdminApiAccessTokenProvider(config);
        var handler = new AdminApiAuthorizationHandler(tokenProvider) {
            InnerHandler = new UnauthorizedHandler(),
        };

        using var client = new HttpClient(handler);

        UnauthorizedAccessException exception = await Should.ThrowAsync<UnauthorizedAccessException>(
            () => client.GetAsync("https://localhost/api/test"));

        exception.Message.ShouldContain("unauthorized");
    }

    private sealed class TestHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            // Verify Bearer token is attached
            _ = request.Headers.Authorization.ShouldNotBeNull();
            request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
            request.Headers.Authorization.Parameter.ShouldNotBeNullOrEmpty();

            // Verify it looks like a JWT (3 parts separated by dots)
            string[] parts = request.Headers.Authorization.Parameter!.Split('.');
            parts.Length.ShouldBe(3);

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private sealed class UnauthorizedHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
    }
}
