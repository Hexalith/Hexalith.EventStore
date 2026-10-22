using Hexalith.EventStore.Client.Handlers;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Handlers;

public sealed class DaprServiceInvocationHandlerTests {
    [Fact]
    public async Task SendAsync_WithConflictingHeaders_ReplacesThemWithAuthoritativeValues() {
        var terminal = new CaptureHandler();
        using var handler = new DaprServiceInvocationHandler("eventstore", "secret-token") {
            InnerHandler = terminal,
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/queries");
        _ = request.Headers.TryAddWithoutValidation("dapr-app-id", "untrusted-app");
        _ = request.Headers.TryAddWithoutValidation("dapr-api-token", "untrusted-token");

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        HttpRequestMessage captured = terminal.Request.ShouldNotBeNull();
        captured.Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
        captured.Headers.GetValues("dapr-api-token").ShouldBe(["secret-token"]);
    }

    [Fact]
    public async Task SendAsync_WithoutConfiguredToken_RemovesSeededToken() {
        var terminal = new CaptureHandler();
        using var handler = new DaprServiceInvocationHandler("eventstore", apiToken: null) {
            InnerHandler = terminal,
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/queries");
        _ = request.Headers.TryAddWithoutValidation("dapr-api-token", "untrusted-token");

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        terminal.Request.ShouldNotBeNull().Headers.Contains("dapr-api-token").ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WithCleanRequest_AddsSingleAuthoritativeValues() {
        var terminal = new CaptureHandler();
        using var handler = new DaprServiceInvocationHandler("eventstore", "secret-token") {
            InnerHandler = terminal,
        };
        using var invoker = new HttpMessageInvoker(handler);

        using HttpResponseMessage response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/queries"),
            TestContext.Current.CancellationToken);

        HttpRequestMessage captured = terminal.Request.ShouldNotBeNull();
        captured.Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
        captured.Headers.GetValues("dapr-api-token").ShouldBe(["secret-token"]);
    }

    private sealed class CaptureHandler : HttpMessageHandler {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
