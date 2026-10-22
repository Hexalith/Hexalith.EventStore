using Hexalith.EventStore.Client.Registration;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Registration;

public sealed class DaprServiceInvocationRegistrationTests {
    [Fact]
    public async Task AddEventStoreDaprServiceInvocation_AfterForwardingHandler_RunsInnermost() {
        var terminal = new CaptureHandler();
        var services = new ServiceCollection();
        _ = services.AddTransient<ConflictingHeaderHandler>();
        _ = services.AddHttpClient("test", client => client.BaseAddress = new Uri("http://localhost"))
            .AddHttpMessageHandler<ConflictingHeaderHandler>()
            .AddEventStoreDaprServiceInvocation("eventstore", "secret-token")
            .ConfigurePrimaryHttpMessageHandler(() => terminal);

        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/queries",
            TestContext.Current.CancellationToken);

        HttpRequestMessage request = terminal.Request.ShouldNotBeNull();
        request.Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
        request.Headers.GetValues("dapr-api-token").ShouldBe(["secret-token"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddEventStoreDaprServiceInvocation_WithBlankAppId_ThrowsArgumentException(string appId) {
        IHttpClientBuilder builder = new ServiceCollection().AddHttpClient("test");

        _ = Should.Throw<ArgumentException>(
            () => builder.AddEventStoreDaprServiceInvocation(appId));
    }

    private sealed class ConflictingHeaderHandler : DelegatingHandler {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            _ = request.Headers.TryAddWithoutValidation("dapr-app-id", "untrusted-app");
            _ = request.Headers.TryAddWithoutValidation("dapr-api-token", "untrusted-token");
            return base.SendAsync(request, cancellationToken);
        }
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
