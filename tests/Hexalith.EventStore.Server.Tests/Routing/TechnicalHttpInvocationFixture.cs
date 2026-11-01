using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Actual Dapr request construction and production IHttpClientFactory sink over sockets, with an explicit HTTP sidecar bridge.</summary>
internal sealed class TechnicalHttpInvocationFixture : IAsyncDisposable
{
    private WebApplication _bridge = null!;
    private ServiceProvider _caller = null!;
    private DaprClient _dapr = null!;
    public TechnicalHttpCatalogFixture Receiver { get; private set; } = null!;
    public RouteCatalogTestFixture Caller { get; private set; } = null!;
    public DaprStorageCommandService Service { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public Uri Address { get; private set; } = null!;
    public int WireRequests { get; private set; }
    public int RedirectedRequests { get; private set; }
    public string? Body { get; private set; }
    public string? Bearer { get; private set; }
    public string? Binding { get; private set; }
    public string? Verb { get; private set; }

    public async Task StartAsync(string scenario, CancellationToken token, bool legacyFinite = false)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        _bridge = builder.Build();
        _bridge.MapMethods("/{**path}", ["GET", "POST", "PUT", "DELETE", "PATCH"], async context =>
        {
            WireRequests++;
            if (context.Request.Path == "/redirected") { RedirectedRequests++; context.Response.StatusCode = 204; return; }
            Verb = context.Request.Method; Bearer = context.Request.Headers.Authorization.ToString(); Binding = context.Request.Headers[RouteCatalogInvocationHandler.BindingHeader];
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8); Body = await reader.ReadToEndAsync(token);
            if (scenario == "redirect") { context.Response.StatusCode = 307; context.Response.Headers.Location = "/redirected"; return; }
            const string prefix = "/v1.0/invoke/eventstore/method/";
            if (!context.Request.Path.Value!.StartsWith(prefix, StringComparison.Ordinal)) { context.Response.StatusCode = 404; return; }
            string path = "/" + context.Request.Path.Value[prefix.Length..] + context.Request.QueryString;
            using var json = JsonDocument.Parse(Body);
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path) { Content = JsonContent.Create(json.RootElement) };
            foreach (var header in context.Request.Headers.Where(header => header.Key.StartsWith("X-Hexalith-", StringComparison.OrdinalIgnoreCase) || header.Key == "Authorization"))
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            if (scenario == "missing-binding") request.Headers.Remove(RouteCatalogInvocationHandler.BindingHeader);
            if (scenario == "tampered-binding")
            {
                request.Headers.Remove(RouteCatalogInvocationHandler.BindingHeader);
                request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.BindingHeader, new string('0', 64));
            }
            using var response = await Receiver.Client.SendAsync(request, token);
            context.Response.StatusCode = (int)response.StatusCode;
            // This explicit test bridge carries only the SDK JSON protocol. Its response
            // must never adopt a content type influenced by the forwarded request.
            context.Response.ContentType = "application/json";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await response.Content.CopyToAsync(context.Response.Body, token);
        });
        await _bridge.StartAsync(token); Address = new Uri(_bridge.Urls.Single());
        var coordinates = new RouteCatalogStateCoordinates("route-catalog", "route-catalog-authority-v1", Address.ToString());
        Receiver = new TechnicalHttpCatalogFixture(coordinates);
        await Receiver.StartAsync(token, fullInventory: !legacyFinite, inventoryAssembly: "Hexalith.EventStore.Gateway", attempt: scenario == "cross-run" ? "other-run" : null,
            invocations: legacyFinite ? [new("eventstore-admin-server", "/api/v1/admin/storage/snapshot-policy", ["PUT"])]
                : [new("eventstore-admin-server", "/**", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"])], legacyFinite: legacyFinite);
        Caller = new RouteCatalogTestFixture(coordinates, ["eventstore-admin-server"], compiledSchemaThree: true);
        Caller.UpdateObservation(0, Receiver.Catalog.Observations[0]);
        Caller.Recapture = actual => actual.HostId == "eventstore" ? actual with { ReceiverSurface = legacyFinite ? Receiver.App.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe()
            : Receiver.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>().Observe() } : actual;
        if (scenario != "inactive")
        {
            var candidate = Caller.Candidate();
            if (legacyFinite) candidate = candidate with { AuxiliaryOperations = Receiver.Catalog.Observations[0].AuxiliaryOperations };
            candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
            await Caller.ActivateAsync(candidate);
        }
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IRouteCatalogAuthority>(Caller.Authorities[Array.FindIndex(Caller.Hosts, host => host.AppId == "eventstore-admin-server")]);
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        services.Configure<RouteCatalogHostOptions>(options => options.RequireTechnicalHttpBinding = !legacyFinite);
        services.AddHttpClient(); _caller = services.BuildServiceProvider(); Client = _caller.GetRequiredService<IHttpClientFactory>().CreateClient();
        var endpoint = new UriBuilder(Address);
        if (scenario == "wrong-host") endpoint.Host = "localhost";
        if (scenario == "wrong-port") endpoint.Port++;
        if (scenario == "wrong-scheme") endpoint.Scheme = "https";
        _dapr = new DaprClientBuilder().UseHttpEndpoint(endpoint.Uri.ToString()).Build();
        var auth = Substitute.For<IAdminAuthContext>(); auth.GetToken().Returns(Receiver.Client.DefaultRequestHeaders.Authorization!.Parameter);
        Service = new DaprStorageCommandService(_dapr, _caller.GetRequiredService<IHttpClientFactory>(),
            Options.Create(new AdminServerOptions { EventStoreAppId = scenario == "wrong-target" ? "different-peer" : "eventstore" }), auth, NullLogger<DaprStorageCommandService>.Instance);
    }

    public HttpRequestMessage Request(HttpMethod method, string path) => new(method, new Uri(Address, path));

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose(); _dapr?.Dispose(); _caller?.Dispose();
        if (_bridge is not null) { await _bridge.StopAsync(); await _bridge.DisposeAsync(); }
        if (Receiver is not null) await Receiver.DisposeAsync();
    }
}
