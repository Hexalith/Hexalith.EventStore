using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Testing.Http;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogTransportIsolationTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, false, true)]
    public async Task OrdinaryRedirectConfigurationIsPreservedWhileDeclaredInvocationCannotRedirect(bool clientHandler, bool redirects, bool failingOwnerFactory, bool ownerNativeDefaults)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var target = fixture.Envelope.Routes.First(r => r.Method == "query");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build(); int ordinaryEffects = 0, declaredEffects = 0, foreignEffects = 0;
        string invocation = $"/v1.0/invoke/{target.AppId}/method/{target.Method}";
        sink.MapPost("/ordinary", () => Results.Redirect("/ordinary-complete", preserveMethod: true));
        sink.MapPost("/ordinary-complete", () => { ordinaryEffects++; return Results.NoContent(); });
        sink.MapPost(invocation, (HttpContext context) =>
        {
            context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldBe(fixture.Candidate().RootDigest);
            declaredEffects++;
            return context.Request.Query.ContainsKey("redirect")
                ? Results.Redirect("/v1.0/invoke/foreign/method/unknown", preserveMethod: true) : Results.NoContent();
        });
        sink.MapPost("/v1.0/invoke/foreign/method/unknown", () => { foreignEffects++; return Results.NoContent(); });
        await sink.StartAsync();
        HttpMessageHandler primary = clientHandler ? new HttpClientHandler { AllowAutoRedirect = redirects }
            : new SocketsHttpHandler { AllowAutoRedirect = redirects };
        var services = new ServiceCollection(); services.AddSingleton<IRouteCatalogAuthority>(fixture.Authorities[0]);
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        if (failingOwnerFactory) services.Configure<RouteCatalogHostOptions>(options => options.InvocationTransportFactory = _ => throw new InvalidOperationException("owner factory failed locally"));
        if (ownerNativeDefaults) services.Configure<RouteCatalogHostOptions>(options => options.InvocationTransportFactory = _ => new SocketsHttpHandler());
        services.AddHttpClient("isolation", c => c.BaseAddress = new Uri(sink.Urls.Single())).ConfigurePrimaryHttpMessageHandler(() => primary);
        using var provider = services.BuildServiceProvider(); using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("isolation");
        using var ordinary = new HttpRequestMessage(HttpMethod.Post, "/ordinary");
        ordinary.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String("ordinary-user:password"u8));
        using var ordinaryResponse = await client.SendAsync(ordinary);
        ordinaryResponse.StatusCode.ShouldBe(redirects ? HttpStatusCode.NoContent : HttpStatusCode.TemporaryRedirect);
        ordinaryEffects.ShouldBe(redirects ? 1 : 0);
        if (failingOwnerFactory) await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.PostAsync(invocation, null));
        else
        {
            using var declared = await client.PostAsync(invocation, null); declared.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            using var redirected = await client.PostAsync(invocation + "?redirect=true", null); redirected.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        }
        declaredEffects.ShouldBe(failingOwnerFactory ? 0 : 2); foreignEffects.ShouldBe(0);
        (primary is HttpClientHandler handler ? handler.AllowAutoRedirect : ((SocketsHttpHandler)primary).AllowAutoRedirect).ShouldBe(redirects);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("empty", "dapr-app-id")]
    [InlineData("active", "dapr-app-id")]
    [InlineData("pending", "dapr-app-id")]
    [InlineData("empty", "DAPR-APP-ID")]
    [InlineData("active", "Dapr-App-Id")]
    [InlineData("pending", "DaPr-ApP-iD")]
    public async Task BasicDaprInvocationIsRefusedBeforeAuthorityOrHttpAcrossLifecyclePhases(string phase, string username)
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate();
        if (phase != "empty") await fixture.ActivateAsync(first);
        if (phase == "pending") await fixture.Authorities[0].PrepareAsync(fixture.Candidate(2, first.RootDigest));
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(c => fixture.Authorities[0].AcquireAsync(c.Arg<CancellationToken>()));
        int effects = 0;
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build(); sink.MapPost("/unknown", () => { effects++; return Results.NoContent(); }); await sink.StartAsync();
        using var client = new HttpClient(new RouteCatalogInvocationHandler(authority, new SocketsHttpHandler()));
        using var request = new HttpRequestMessage(HttpMethod.Post, sink.Urls.Single() + "/unknown");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":unknown-target")));
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, "caller-root");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "999");
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.SendAsync(request));
        effects.ShouldBe(0); authority.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ClientCertificateOption.Manual)]
    [InlineData(ClientCertificateOption.Automatic)]
    public void NativeCertificateCredentialProxyAndConnectionSettingsSurviveIsolation(ClientCertificateOption mode)
    {
        var proxy = new WebProxy("http://127.0.0.1:54321"); var credentials = new NetworkCredential("<username>", "<password>");
        using var ordinary = new HttpClientHandler
        {
            AllowAutoRedirect = true, ClientCertificateOptions = mode, Credentials = credentials,
            DefaultProxyCredentials = credentials, Proxy = proxy, UseProxy = true,
            CheckCertificateRevocationList = true, MaxConnectionsPerServer = 7, MaxResponseHeadersLength = 23,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => false,
        };
        using var guarded = RouteCatalogInvocationTransport.Create(ordinary);
        var copy = guarded.ShouldBeOfType<HttpClientHandler>();
        copy.AllowAutoRedirect.ShouldBeFalse(); ordinary.AllowAutoRedirect.ShouldBeTrue();
        copy.ClientCertificateOptions.ShouldBe(mode); copy.Credentials.ShouldBeSameAs(credentials);
        copy.DefaultProxyCredentials.ShouldBeSameAs(credentials); copy.Proxy.ShouldBeSameAs(proxy);
        copy.MaxConnectionsPerServer.ShouldBe(7); copy.MaxResponseHeadersLength.ShouldBe(23);
        copy.CheckCertificateRevocationList.ShouldBeTrue();
        copy.ServerCertificateCustomValidationCallback.ShouldBeSameAs(ordinary.ServerCertificateCustomValidationCallback);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomTransportNeedsExplicitSeparateOwnerConfigurationOnlyForInvocation(bool configured)
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var target = fixture.Envelope.Routes.First(r => r.Method == "query"); int ordinary = 0, invocation = 0;
        var options = Options.Create(new RouteCatalogHostOptions
        {
            InvocationTransportFactory = configured ? _ => new MockHttpMessageHandler(_ => { invocation++; return new(HttpStatusCode.NoContent); }) : null,
        });
        using var client = new HttpClient(new RouteCatalogInvocationHandler(fixture.Authorities[0],
            new MockHttpMessageHandler(_ => { ordinary++; return new(HttpStatusCode.Accepted); }), options));
        using var ordinaryResponse = await client.GetAsync("http://fixture/ordinary"); ordinaryResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        string address = $"http://fixture/v1.0/invoke/{target.AppId}/method/{target.Method}";
        if (configured) { using var response = await client.PostAsync(address, null); response.StatusCode.ShouldBe(HttpStatusCode.NoContent); }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.PostAsync(address, null));
        ordinary.ShouldBe(1); invocation.ShouldBe(configured ? 1 : 0);
    }

    [Theory]
    [InlineData("undeclared", false)]
    [InlineData("undeclared", true)]
    [InlineData("actor", false)]
    [InlineData("declared", false)]
    [InlineData("declared-then-foreign", false)]
    [InlineData("chain-undeclared", false)]
    [InlineData("empty", false)]
    [InlineData("pending", false)]
    [InlineData("disabled", false)]
    public async Task OrdinaryRedirectFencesLaterDaprHops(string scenario, bool clientHandler)
    {
        var fixture = new RouteCatalogTestFixture();
        if (scenario is not "empty") await fixture.ActivateAsync(fixture.Candidate());
        if (scenario == "pending") await fixture.Authorities[0].PrepareAsync(fixture.Candidate(2, fixture.Candidate().RootDigest));
        var target = fixture.Envelope.Routes.First(r => r.Method == "query");
        string declaredPath = $"/v1.0/invoke/{target.AppId}/method/{target.Method}";
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(c => fixture.Authorities[0].AcquireAsync(c.Arg<CancellationToken>()));
        int undeclared = 0, actors = 0, declared = 0, foreign = 0, middle = 0;
        string? forwardedRoot = null;
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build();
        sink.MapPost("/ordinary-undeclared", () => Results.Redirect("/v1.0/invoke/undeclared/method/unknown", preserveMethod: true));
        sink.MapPost("/ordinary-actor", () => Results.Redirect("/v1.0/actors/UndeclaredActor/id/method/Unknown", preserveMethod: true));
        sink.MapPost("/ordinary-declared", () => Results.Redirect(declaredPath, preserveMethod: true));
        sink.MapPost("/ordinary-foreign", () => Results.Redirect(declaredPath + "?redirect=true", preserveMethod: true));
        sink.MapPost("/ordinary-chain", () => Results.Redirect("/ordinary-middle", preserveMethod: true));
        sink.MapPost("/ordinary-middle", () => { middle++; return Results.Redirect("/v1.0/invoke/undeclared/method/unknown", preserveMethod: true); });
        sink.MapPost("/v1.0/invoke/undeclared/method/unknown", (HttpContext context) =>
        {
            forwardedRoot = context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString();
            undeclared++; return Results.NoContent();
        });
        sink.MapPost("/v1.0/actors/UndeclaredActor/id/method/Unknown", () => { actors++; return Results.NoContent(); });
        sink.MapPost("/v1.0/invoke/foreign/method/unknown", () => { foreign++; return Results.NoContent(); });
        sink.MapPost(declaredPath, (HttpContext context) =>
        {
            context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldBe(fixture.Candidate().RootDigest);
            context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString().ShouldNotBe("caller-root");
            declared++;
            return context.Request.Query.ContainsKey("redirect")
                ? Results.Redirect("/v1.0/invoke/foreign/method/unknown", preserveMethod: true) : Results.NoContent();
        });
        await sink.StartAsync();
        bool redirects = scenario != "disabled";
        HttpMessageHandler primary = clientHandler ? new HttpClientHandler { AllowAutoRedirect = redirects }
            : new SocketsHttpHandler { AllowAutoRedirect = redirects };
        var services = new ServiceCollection(); services.AddSingleton<IRouteCatalogAuthority>(authority); services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        services.AddHttpClient("isolation", client => client.BaseAddress = new Uri(sink.Urls.Single())).ConfigurePrimaryHttpMessageHandler(() => primary);
        using var provider = services.BuildServiceProvider(); using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("isolation");
        string path = scenario switch
        {
            "actor" => "/ordinary-actor",
            "declared" => "/ordinary-declared",
            "declared-then-foreign" => "/ordinary-foreign",
            "chain-undeclared" => "/ordinary-chain",
            _ => "/ordinary-undeclared",
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, "caller-root");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "999");
        if (scenario is "declared" or "declared-then-foreign")
        {
            using var response = await client.SendAsync(request);
            response.StatusCode.ShouldBe(scenario == "declared" ? HttpStatusCode.NoContent : HttpStatusCode.TemporaryRedirect);
        }
        else if (scenario == "disabled")
        {
            using var response = await client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        }
        else
        {
            var exception = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.SendAsync(request));
            exception.Message.ShouldContain(scenario == "actor"
                ? "Raw actor invocation"
                : scenario is "empty" or "pending"
                    ? "No complete catalog generation is serving."
                    : "outside the committed catalog");
        }
        undeclared.ShouldBe(0); actors.ShouldBe(0); foreign.ShouldBe(0); forwardedRoot.ShouldBeNull();
        declared.ShouldBe(scenario is "declared" or "declared-then-foreign" ? 1 : 0);
        middle.ShouldBe(scenario == "chain-undeclared" ? 1 : 0);
        authority.ReceivedCalls().Count().ShouldBe(scenario is "disabled" or "actor" ? 0 : 1);
        (primary is HttpClientHandler handler ? handler.AllowAutoRedirect : ((SocketsHttpHandler)primary).AllowAutoRedirect).ShouldBe(redirects);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrdinaryAutomaticRedirectPreservesNativeMethodAndBodyRules()
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        string? landedMethod = null, landedBody = null;
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build();
        sink.MapPost("/found", () => Results.Redirect("/landed-get"));
        sink.MapGet("/landed-get", (HttpContext context) => { landedMethod = context.Request.Method; return Results.NoContent(); });
        sink.MapPost("/keep", () => Results.Redirect("/landed-body", preserveMethod: true));
        sink.MapPost("/landed-body", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            landedBody = await reader.ReadToEndAsync();
            landedMethod = context.Request.Method;
            return Results.NoContent();
        });
        await sink.StartAsync();
        var services = new ServiceCollection(); services.AddSingleton<IRouteCatalogAuthority>(authority); services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        services.AddHttpClient("isolation", client => client.BaseAddress = new Uri(sink.Urls.Single()))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = true });
        using var provider = services.BuildServiceProvider(); using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("isolation");
        using var found = new HttpRequestMessage(HttpMethod.Post, "/found") { Content = new StringContent("drop-me") };
        using var foundResponse = await client.SendAsync(found);
        foundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent); landedMethod.ShouldBe("GET");
        using var keep = new HttpRequestMessage(HttpMethod.Post, "/keep") { Content = new StringContent("replay-me") };
        using var keepResponse = await client.SendAsync(keep);
        keepResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent); landedMethod.ShouldBe("POST"); landedBody.ShouldBe("replay-me");
        authority.ReceivedCalls().ShouldBeEmpty();
    }
}
