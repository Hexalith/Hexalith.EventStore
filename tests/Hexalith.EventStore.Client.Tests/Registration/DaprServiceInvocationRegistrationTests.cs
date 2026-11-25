using Hexalith.EventStore.Client.Registration;

using Microsoft.Extensions.Configuration;
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

    [Fact]
    public async Task FactoriesUseDependencyInjectionOncePerSend()
    {
        string rotatedToken = Guid.NewGuid().ToString("N");
        var settings = new Dictionary<string, string?>
        {
            ["target"] = "eventstore",
            ["token"] = Guid.NewGuid().ToString("N"),
        };
        var terminal = new CaptureHandler();
        int stateReads = 0;
        var services = new ServiceCollection();
        services.AddSingleton(settings);
        services.AddTransient<ConflictingHeaderHandler>();
        services.AddHttpClient("test", client => client.BaseAddress = new Uri("http://localhost"))
            .AddHttpMessageHandler<ConflictingHeaderHandler>()
            .AddEventStoreDaprServiceInvocation(
                provider =>
                {
                    stateReads++;
                    return provider.GetRequiredService<Dictionary<string, string?>>();
                },
                state => state["target"]!,
                state => state["token"])
            .ConfigurePrimaryHttpMessageHandler(() => terminal);
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");
        foreach (string? token in new[] { settings["token"], rotatedToken, null })
        {
            settings["token"] = token;
            using HttpResponseMessage response = await client.GetAsync("/api/v1/queries", TestContext.Current.CancellationToken);
            terminal.Request.ShouldNotBeNull().Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
            if (token is null) terminal.Request.Headers.Contains("dapr-api-token").ShouldBeFalse();
            else terminal.Request.Headers.GetValues("dapr-api-token").ShouldBe([token]);
        }
        stateReads.ShouldBe(3);
    }

    [Theory]
    [InlineData("http://127.0.0.1:3789", "", "http://localhost:3789/api/", false)]
    [InlineData("", "3789", "http://127.0.0.1:3789", false)]
    [InlineData("invalid", "3789", "http://127.0.0.1:3789", false)]
    [InlineData("https://dapr.example.test:3789", "", "https://dapr.example.test:3789", false)]
    [InlineData("http://127.0.0.1:3789", "", "http://localhost:3790", true)]
    [InlineData("http://127.0.0.1:3789", "", "http://127.0.0.2:3789", true)]
    public async Task GatewayClient_RequiresRoutingOnlyForConfiguredSidecar(
        string endpoint, string port, string baseAddress, bool directGateway)
    {
        var terminal = new CaptureHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = endpoint, ["DAPR_HTTP_PORT"] = port }).Build());
        IHttpClientBuilder builder = services.AddEventStoreGatewayClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri(baseAddress))
            .ConfigurePrimaryHttpMessageHandler(() => terminal);
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(builder.Name);

        if (directGateway)
        {
            using HttpResponseMessage response = await client.GetAsync("api/v1/queries", TestContext.Current.CancellationToken);
            terminal.Request.ShouldNotBeNull();
        }
        else
        {
            await AssertRoutingFailureAsync(client);
            terminal.Request.ShouldBeNull();
        }
    }

    [Fact]
    public async Task GatewayClient_RefusesRoutingBeforeForwardingHandlers()
    {
        const string endpoint = "http://dapr.example.test:3789";
        var terminal = new CaptureHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = endpoint }).Build());
        services.AddTransient<ConflictingHeaderHandler>();
        IHttpClientBuilder builder = services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri(endpoint))
            .AddEventStoreDaprServiceInvocation("eventstore")
            .AddHttpMessageHandler<ConflictingHeaderHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => terminal);
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(builder.Name);

        await AssertRoutingFailureAsync(client);

        terminal.Request.ShouldBeNull();
    }

    private static async Task AssertRoutingFailureAsync(HttpClient client)
    {
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(
            () => client.GetAsync("api/v1/queries", TestContext.Current.CancellationToken));
        error.Message.ShouldContain("AddEventStoreDaprServiceInvocation");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GatewayClient_AcceptsBothRoutingRegistrationsForConfiguredSidecar(bool dynamicRouting)
    {
        const string endpoint = "http://dapr.example.test:3789";
        var terminal = new CaptureHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = endpoint }).Build());
        IHttpClientBuilder client = services.AddEventStoreGatewayClient(options => options.BaseAddress = new Uri(endpoint));
        if (dynamicRouting)
        {
            client.AddEventStoreDaprServiceInvocation(_ => "eventstore", app => app, _ => null);
        }
        else
        {
            client.AddEventStoreDaprServiceInvocation("eventstore");
        }
        client.ConfigurePrimaryHttpMessageHandler(() => terminal);
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(client.Name);

        using HttpResponseMessage response = await http.GetAsync("api/v1/queries", TestContext.Current.CancellationToken);

        terminal.Request.ShouldNotBeNull().Headers.GetValues("dapr-app-id").ShouldBe(["eventstore"]);
    }

    [Fact]
    public void FixedOverloadRetainsLiteralNullArgumentValidation()
    {
        IHttpClientBuilder builder = new ServiceCollection().AddHttpClient("test");
        Should.Throw<ArgumentNullException>(() => builder.AddEventStoreDaprServiceInvocation(null!));
        Should.Throw<ArgumentNullException>(() => builder.AddEventStoreDaprServiceInvocation(null!, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddEventStoreDaprServiceInvocation_WithBlankAppId_ThrowsArgumentException(string appId) {
        IHttpClientBuilder builder = new ServiceCollection().AddHttpClient("test");

        _ = Should.Throw<ArgumentException>(
            () => builder.AddEventStoreDaprServiceInvocation(appId));
    }

    [Theory]
    [InlineData("app\r\nid")]
    [InlineData("app id")]
    [InlineData("app/id")]
    public void AddEventStoreDaprServiceInvocation_WithMalformedAppId_ThrowsArgumentException(string appId) {
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
