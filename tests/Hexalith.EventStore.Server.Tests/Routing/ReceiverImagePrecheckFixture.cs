using System.Reflection;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Executes actual receiver middleware and default DI attachment without starting a host or contacting a sidecar.</summary>
internal sealed class ReceiverImagePrecheckFixture : IAsyncDisposable
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Channel = "receiver-image-fixture-channel";
    private readonly string _directory = Directory.CreateTempSubdirectory("receiver-image-").FullName;
    private readonly RequestDelegate _pipeline;
    internal WebApplication App { get; }
    internal TechnicalHttpRouteCatalogReceiver Receiver { get; }
    internal ReceiverImageActorOptions Registrations { get; }
    internal RouteCatalogHostOptions Options { get; }
    internal IRouteCatalogStateStore Store { get; } = Substitute.For<IRouteCatalogStateStore>();
    internal IRouteCatalogTrustValidator Trust { get; } = Substitute.For<IRouteCatalogTrustValidator>();
    internal int StoreConstructions { get; private set; }
    internal int StateReads { get; private set; }
    internal int NextCalls { get; private set; }
    internal Action<HttpContext>? OnNext { get; set; }
    internal Action? OnStateRead { get; set; }
    internal RouteCatalogImageDigestScope? StateReadImageScope { get; private set; }

    internal ReceiverImagePrecheckFixture(bool customAuthority = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile"] = RouteCatalogTechnicalHttpPolicy.FenceVersion;
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = _directory;
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = Channel;
        builder.Services.AddSingleton<IRouteCatalogStateStore>(_ => { StoreConstructions++; return Store; });
        builder.Services.AddSingleton(Trust);
        if (customAuthority) builder.Services.AddSingleton(Substitute.For<IRouteCatalogAuthority>());
        builder.Services.AddEventStoreRouteCatalog(builder.Configuration);
        builder.Services.Configure<RouteCatalogHostOptions>(options =>
        {
            options.Identity = new("receiver-image", "eventstore", "inventory", "topology", "authentication");
            options.Profile = "Development"; options.DevelopmentFixture = true;
            options.ScopedDecodeReuse = true; options.RetainValidatedCatalogBytes = true; options.RequireTechnicalHttpBinding = true;
        });
        builder.Services.AddActors(options =>
        {
            options.Actors.RegisterActor<AggregateActor>();
            options.Actors.RegisterActor<ETagActor>();
        });
        builder.Services.AddHealthChecks();
        builder.Services.AddEventStoreHttpHostSecurity();
        builder.Services.AddSingleton<ReceiverImageActorOptions>();
        builder.Services.AddSingleton<IDevelopmentNativeActorReceiver>(provider => new DevelopmentNativeActorReceiver(
            provider.GetRequiredService<ActorRuntime>(), provider.GetRequiredService<ReceiverImageActorOptions>(),
            provider.GetRequiredService<HealthCheckService>(), builder.Configuration));
        App = builder.Build();
        App.MapActorsHandlers().RequireEventStoreChannel();
        TechnicalHttpRouteCatalogReceiver.UseDevelopmentTechnicalHttpCatalogReceiver(App, _ => null);
        App.Run(context =>
        {
            NextCalls++; OnNext?.Invoke(context); context.Response.StatusCode = 204; return Task.CompletedTask;
        });
        _pipeline = ((IApplicationBuilder)App).Build();
        Receiver = App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
        Registrations = App.Services.GetRequiredService<ReceiverImageActorOptions>();
        Options = App.Services.GetRequiredService<IOptions<RouteCatalogHostOptions>>().Value;
        Store.ReadAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            StateReads++; StateReadImageScope = RouteCatalogImageDigestScope.Current; OnStateRead?.Invoke();
            return Task.FromException<(RouteCatalogActivationState, string)>(new RouteCatalogUnavailableException("Fixture stops after the original fresh authority read."));
        });
        Registrations.Reset();
    }

    internal RouteCatalogAuthority ResolveOwner() => (RouteCatalogAuthority)App.Services.GetRequiredService<IRouteCatalogAuthority>();
    internal static RouteCatalogImageDigestMemo Memo(RouteCatalogAuthority owner)
        => (RouteCatalogImageDigestMemo)typeof(RouteCatalogAuthority).GetField("_imageDigests", Private)!.GetValue(owner)!;

    internal RouteEndpoint Endpoint(string path) => ((IEndpointRouteBuilder)App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
        .Single(endpoint => endpoint.RoutePattern.RawText!.Trim('/') == path.Trim('/'));

    internal async Task<HttpContext> RequestAsync(string path = "dapr/config", bool validChannel = true)
    {
        var context = new DefaultHttpContext { RequestServices = App.Services };
        var endpoint = Endpoint(path); context.SetEndpoint(endpoint);
        context.Request.Method = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? "GET";
        context.Request.Headers[DaprAppChannelToken.HeaderName] = validChannel ? Channel : "different-channel";
        context.Request.RouteValues["actorTypeName"] = "AggregateActor";
        context.Request.RouteValues["actorId"] = "owned-test-actor";
        context.Request.RouteValues["methodName"] = "ProcessCommandAsync";
        await _pipeline(context);
        return context;
    }

    internal void CaptureNativeBaseline()
    {
        // Populate only the real native baseline through the production fingerprint/Complete seam;
        // full startup/MVC capture is independently covered by TechnicalHttpCatalogFixture tests.
        var pass = new TechnicalHttpObservationPass(App.Services);
        var captured = (Dictionary<RouteEndpoint, string>)typeof(TechnicalHttpRouteCatalogReceiver).GetField("_captured", Private)!.GetValue(Receiver)!;
        var fingerprint = typeof(TechnicalHttpRouteCatalogReceiver).GetMethod("Fingerprint", Private)!;
        foreach (var endpoint in ((IEndpointRouteBuilder)App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>())
            captured[endpoint] = (string)fingerprint.Invoke(Receiver, [endpoint, pass, null])!;
        pass.Complete();
        ((TaskCompletionSource)typeof(TechnicalHttpRouteCatalogReceiver).GetField("_ready", Private)!.GetValue(Receiver)!).TrySetResult();
        Registrations.Reset();
    }

    public async ValueTask DisposeAsync()
    {
        await App.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }
}
