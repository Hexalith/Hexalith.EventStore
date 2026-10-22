using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Configuration;

using Dapr.Actors.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

public sealed partial class DaprTestContainerFixture
{
    private static void ConfigureGuardedCounter(WebApplicationBuilder builder)
    {
        string? receipts = Environment.GetEnvironmentVariable("EVENTSTORE_TEST_DELIVERY_RECEIPT_LIMIT");
        string? attempts = Environment.GetEnvironmentVariable("EVENTSTORE_TEST_DELIVERY_CAS_ATTEMPTS");
        if (receipts is not null || attempts is not null)
        {
            var limits = new ProjectionDeliveryIdempotencyOptions
            {
                CompletedReceiptLimit = ParseGuardFixtureLimit(receipts, 256),
                MaxStateTransitionAttempts = ParseGuardFixtureLimit(attempts, 8),
            };
            limits.Validate();
            builder.Services.AddSingleton<IOptions<ProjectionDeliveryIdempotencyOptions>>(Options.Create(limits));
        }
        builder.Services.AddScoped<IAsyncDomainProjectionHandler, LiveGuardedCounterProjectionHandler>();
        builder.Services.AddSingleton<LiveGuardedCounterFaultControl>();
        builder.Services.AddScoped<IAsyncDomainProjectionHandler, LiveReplayCounterProjectionHandler>();
        builder.Services.AddSingleton<LiveReplayCounterFaultControl>();
        builder.Services.AddSingleton(new DomainProjectionReplayRecoveryRegistration(LiveReplayCounterProjectionHandler.SourceDomain,
            LiveReplayCounterProjectionHandler.Route, LiveReplayCounterProjectionHandler.Store));
        builder.Services.AddSingleton<LiveNativeCallbackCounter>();
        builder.Services.AddSingleton<LiveActorSendCounter>();
        builder.Services.AddSingleton<LiveActorSendHandler>();
        builder.Services.AddSingleton(_ => new LiveSdkHttpAttemptObserver(int.Parse(builder.Configuration["Dapr:HttpPort"]!,
            System.Globalization.CultureInfo.InvariantCulture)));
        builder.Services.AddSingleton<IActorProxyFactory>(provider => new ActorProxyFactory(new ActorProxyOptions
            { HttpEndpoint = "http://127.0.0.1:" + builder.Configuration["Dapr:HttpPort"] },
            provider.GetRequiredService<LiveActorSendHandler>()));
        builder.Services.AddSingleton(new DomainProjectionOwnerProofRegistration(
            LiveGuardedCounterProjectionHandler.SourceDomain, LiveGuardedCounterProjectionHandler.Route,
            LiveGuardedCounterProjectionHandler.Capability,
            new(LiveGuardedCounterProjectionHandler.Store, LiveGuardedCounterProjectionHandler.Route)));
    }

    private static int ParseGuardFixtureLimit(string? value, int fallback)
        => value is null ? fallback : int.TryParse(value, System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out int parsed) ? parsed
            : throw new InvalidOperationException("Invalid source guard fixture profile.");

    internal ActorProxyFactory CreateObservedActorProxyFactory()
        => new(new ActorProxyOptions { HttpEndpoint = DaprHttpEndpoint },
            new LiveActorSendHandler(Services.GetRequiredService<LiveActorSendCounter>()));

    private static void MapGuardedCounterProof(WebApplication host)
    {
        _ = host.Services.GetRequiredService<LiveSdkHttpAttemptObserver>();
        host.Use(async (context, next) =>
        {
            LiveNativeCallbackCounter counter = host.Services.GetRequiredService<LiveNativeCallbackCounter>();
            counter.Observe(context.Request.Path.Value ?? string.Empty);
            if (context.Request.Path != "/admin/operational-index-metadata")
            { await next(context).ConfigureAwait(false); return; }
            Stream request = context.Request.Body, response = context.Response.Body;
            using var input = new LiveByteCountingStream(request);
            using var output = new LiveByteCountingStream(response);
            context.Request.Body = input;
            context.Response.Body = output;
            try { await next(context).ConfigureAwait(false); }
            finally
            {
                context.Request.Body = request;
                context.Response.Body = response;
                counter.ObserveMetadataBodies(input.Bytes, output.Bytes);
            }
        });
        host.MapPost("/project/source-guard/owner-proof/v1", async (ProjectionOwnerProofRequest request,
            IServiceProvider services, DomainProjectionCatalogRegistry catalog, CancellationToken cancellationToken)
            => Results.Ok(await DomainProjectionOwnerProof.ReadAsync(services, request, catalog, cancellationToken).ConfigureAwait(false)));
        host.MapPost("/project/source-guard/applied-receipt/v1", async (ProjectionAppliedReceiptRequest request,
            IServiceProvider services, DomainProjectionCatalogRegistry catalog, CancellationToken cancellationToken)
            => Results.Ok(await DomainProjectionOwnerProof.ReadAppliedReceiptAsync(services, request, catalog, cancellationToken).ConfigureAwait(false)));
        host.MapPost("/project/source-guard/reconcile-applied-receipt/v1", async (ProjectionAppliedReceiptRequest request,
            IServiceProvider services, DomainProjectionCatalogRegistry catalog, CancellationToken cancellationToken)
            => Results.Ok(await DomainProjectionOwnerProof.ReconcileAppliedReceiptAsync(services, request, catalog, cancellationToken).ConfigureAwait(false)));
        host.MapPost("/project/rebuild/reclaim-guarded/v1", async (ProjectionDispatchRequest request, HttpContext context,
            IServiceProvider services, IOptions<ProjectionDispatchOptions> options,
            IOptions<DomainProjectionIdentityOptions> identity, CancellationToken cancellationToken)
            => Results.Ok(await DomainProjectionGuardedReplay.ReclaimAsync(services, DomainProjectionReplayContext.Bind(request, context.Request.Headers),
                options.Value, identity.Value, cancellationToken).ConfigureAwait(false)));
        host.MapPost("/project/rebuild/reclaim-recovery/v1", async (ProjectionDispatchRequest request, HttpContext context,
            IServiceProvider services, IOptions<ProjectionDispatchOptions> options,
            IOptions<DomainProjectionIdentityOptions> identity, CancellationToken cancellationToken)
            => Results.Ok(await DomainProjectionReplayRecovery.ExecuteAsync(services, DomainProjectionReplayContext.Bind(request, context.Request.Headers), options.Value, identity.Value,
                DomainProjectionRebuildBatchAction.Reclaim, context.Request.Headers[ProjectionReplayRecoveryBinding.HeaderName].ToString(),
                cancellationToken).ConfigureAwait(false)));
    }
}
