using System.Security.Claims;
using System.Threading.RateLimiting;
using Dapr.Client;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public sealed class EventStoreWorkloadRateLimitTests
{
    [Fact]
    public async Task AssignedConfigurationStoreUpdatesTheEffectiveTenantLimitAsync()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:RateLimiting:ConfigurationStoreName"] = "assigned-rate-limits",
            ["EventStore:RateLimiting:PermitLimit"] = "2",
            ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
        }).Build();
        DaprClient dapr = Substitute.For<DaprClient>();
        Dictionary<string, ConfigurationItem> stored = new()
        {
            ["ratelimit:tenants"] = new("assigned-tenant", "1", new Dictionary<string, string>()),
            ["ratelimit:assigned-tenant:permit-limit"] = new("5", "1", new Dictionary<string, string>()),
        };
        dapr.GetConfiguration(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => new GetConfigurationResponse(call.ArgAt<string>(0) == "assigned-rate-limits"
                ? call.ArgAt<IReadOnlyList<string>>(1).ToDictionary(key => key, key => stored[key]) : new Dictionary<string, ConfigurationItem>()));
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(dapr);
        services.AddLogging();
        services.AddEventStore();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<RateLimitingOptions> limits = provider.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>();
        limits.CurrentValue.TenantPermitLimits.ShouldBeEmpty();
        PartitionedRateLimiter<HttpContext> limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter.ShouldNotBeNull();
        using (RateLimitLease first = limiter.AttemptAcquire(TenantContext(provider, "default-tenant"))) first.IsAcquired.ShouldBeTrue();
        using (RateLimitLease second = limiter.AttemptAcquire(TenantContext(provider, "default-tenant"))) second.IsAcquired.ShouldBeTrue();
        using (RateLimitLease defaultLimit = limiter.AttemptAcquire(TenantContext(provider, "default-tenant"))) defaultLimit.IsAcquired.ShouldBeFalse();
        using DaprRateLimitConfigSync sync = ActivatorUtilities.CreateInstance<DaprRateLimitConfigSync>(provider);
        await sync.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            using CancellationTokenSource bound = new(TimeSpan.FromSeconds(3));
            while (!limits.CurrentValue.TenantPermitLimits.TryGetValue("assigned-tenant", out int value) || value != 5)
                await Task.Delay(10, bound.Token);
            using RateLimitLease assigned = limiter.AttemptAcquire(TenantContext(provider, "assigned-tenant"), 5);
            assigned.IsAcquired.ShouldBeTrue();
            using RateLimitLease exhausted = limiter.AttemptAcquire(TenantContext(provider, "assigned-tenant"));
            exhausted.IsAcquired.ShouldBeFalse();
            await dapr.Received(2).GetConfiguration("assigned-rate-limits", Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        }
        finally { await sync.StopAsync(CancellationToken.None); }
    }

    private static DefaultHttpContext TenantContext(IServiceProvider provider, string tenant)
        => new() { RequestServices = provider, User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("eventstore:tenant", tenant)], "Bearer")) };

    [Fact]
    public void Signed_workload_reads_do_not_share_anonymous_rate_limit_partitions()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:RateLimiting:PermitLimit"] = "2",
            ["EventStore:RateLimiting:WindowSeconds"] = "60",
            ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
            ["EventStore:RateLimiting:ConsumerPermitLimit"] = "2",
            ["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60",
            ["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1"
        }).Build();
        var services = new ServiceCollection();
        _ = services.AddSingleton(configuration);
        _ = services.AddLogging();
        _ = services.AddEventStore();
        using ServiceProvider provider = services.BuildServiceProvider();
        PartitionedRateLimiter<HttpContext> limiter = provider
            .GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter.ShouldNotBeNull();

        using RateLimitLease anonymous1 = limiter.AttemptAcquire(Context(provider, null));
        using RateLimitLease anonymous2 = limiter.AttemptAcquire(Context(provider, null));
        using RateLimitLease anonymous3 = limiter.AttemptAcquire(Context(provider, null));
        anonymous1.IsAcquired.ShouldBeTrue();
        anonymous2.IsAcquired.ShouldBeTrue();
        anonymous3.IsAcquired.ShouldBeFalse();

        using RateLimitLease workload1 = limiter.AttemptAcquire(Context(provider, Workload("timesheets", "fixture-tenant")));
        using RateLimitLease workload2 = limiter.AttemptAcquire(Context(provider, Workload("timesheets", "fixture-tenant")));
        using RateLimitLease workload3 = limiter.AttemptAcquire(Context(provider, Workload("timesheets", "fixture-tenant")));
        workload1.IsAcquired.ShouldBeTrue();
        workload2.IsAcquired.ShouldBeTrue();
        workload3.IsAcquired.ShouldBeFalse();

        // Human JWT claims cannot mint a DaprInternal partition, even when they copy its names.
        var forgedHuman = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, "forged-workload"),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, "forged-tenant")
        ], "Bearer"));
        using RateLimitLease forged = limiter.AttemptAcquire(Context(provider, forgedHuman));
        forged.IsAcquired.ShouldBeFalse();

        using RateLimitLease genuine = limiter.AttemptAcquire(
            Context(provider, Workload("forged-workload", "forged-tenant")));
        genuine.IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public void AuthorityRoutesHaveThreeBoundedPreAuthenticationPartitionsInActualGlobalLimiter()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:RateLimiting:PermitLimit"] = "2", ["EventStore:RateLimiting:WindowSeconds"] = "60",
            ["EventStore:RateLimiting:SegmentsPerWindow"] = "1", ["EventStore:RateLimiting:ConsumerPermitLimit"] = "2",
            ["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60", ["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1",
        }).Build();
        var services = new ServiceCollection(); services.AddSingleton(configuration); services.AddLogging(); services.AddEventStore();
        using ServiceProvider provider = services.BuildServiceProvider();
        PartitionedRateLimiter<HttpContext> limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter.ShouldNotBeNull();
        using RateLimitLease anonymous1 = limiter.AttemptAcquire(Context(provider, null));
        using RateLimitLease anonymous2 = limiter.AttemptAcquire(Context(provider, null));
        using RateLimitLease anonymous3 = limiter.AttemptAcquire(Context(provider, null));
        anonymous1.IsAcquired.ShouldBeTrue(); anonymous2.IsAcquired.ShouldBeTrue(); anonymous3.IsAcquired.ShouldBeFalse();
        foreach (WorkloadAuthorityEndpointMetadata marker in new[] { WorkloadAuthorityEndpointMetadata.Token, WorkloadAuthorityEndpointMetadata.Discovery, WorkloadAuthorityEndpointMetadata.Keys })
        {
            DefaultHttpContext first = Context(provider, null); first.Request.QueryString = new QueryString("?client_id=one");
            first.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(marker), "synthetic-authority-endpoint"));
            DefaultHttpContext second = Context(provider, new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "untrusted-two")], "Bearer")));
            second.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(marker), "synthetic-authority-endpoint"));
            DefaultHttpContext third = Context(provider, new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "untrusted-three")], "Bearer")));
            third.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(marker), "synthetic-authority-endpoint"));
            using RateLimitLease lease1 = limiter.AttemptAcquire(first);
            using RateLimitLease lease2 = limiter.AttemptAcquire(second);
            using RateLimitLease lease3 = limiter.AttemptAcquire(third);
            lease1.IsAcquired.ShouldBeTrue(); lease2.IsAcquired.ShouldBeTrue(); lease3.IsAcquired.ShouldBeFalse();
        }
        DefaultHttpContext spoofedPath = Context(provider, null); spoofedPath.Request.Path = "/workload-authority/token";
        using RateLimitLease spoofed = limiter.AttemptAcquire(spoofedPath);
        spoofed.IsAcquired.ShouldBeFalse();
    }

    private static DefaultHttpContext Context(IServiceProvider provider, ClaimsPrincipal? principal)
        => new() { RequestServices = provider, User = principal ?? new ClaimsPrincipal() };

    private static ClaimsPrincipal Workload(string caller, string tenant)
        => new(new ClaimsIdentity(
        [
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, caller),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant)
        ], DaprInternalAuthenticationOptions.SchemeName));
}
