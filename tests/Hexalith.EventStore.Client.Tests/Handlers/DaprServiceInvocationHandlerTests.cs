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

    [Fact]
    public async Task SendAsyncWithRotatedOrRemovedCredentialResolvesEachSend()
    {
        string firstToken = Guid.NewGuid().ToString("N");
        string rotatedToken = Guid.NewGuid().ToString("N");
        string target = "first";
        string? token = firstToken;
        var terminal = new CaptureHandler();
        using var handler = new DaprServiceInvocationHandler(() => (target, token)) { InnerHandler = terminal };
        using var invoker = new HttpMessageInvoker(handler);
        foreach ((string nextTarget, string? nextToken) in new[] { ("first", firstToken), ("second", rotatedToken), ("second", (string?)null), ("second", "") })
        {
            target = nextTarget;
            token = nextToken;
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/queries");
            request.Headers.TryAddWithoutValidation("dapr-app-id", "forged");
            request.Headers.TryAddWithoutValidation("dapr-api-token", "forged");
            using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);
            request.Headers.GetValues("dapr-app-id").ShouldBe([target]);
            if (string.IsNullOrEmpty(token)) request.Headers.Contains("dapr-api-token").ShouldBeFalse();
            else request.Headers.GetValues("dapr-api-token").ShouldBe([token]);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("wrong target")]
    [InlineData("wrong\r\ntarget")]
    public async Task SendAsyncWithInvalidFactoryTargetRefusesTransport(string? target)
    {
        var terminal = new CaptureHandler();
        using var handler = new DaprServiceInvocationHandler(() => (target!, "secret")) { InnerHandler = terminal };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/queries");
        await Should.ThrowAsync<ArgumentException>(() => invoker.SendAsync(request, TestContext.Current.CancellationToken));
        terminal.Request.ShouldBeNull();
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
