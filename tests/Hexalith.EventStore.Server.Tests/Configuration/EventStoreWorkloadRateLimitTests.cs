using System.Security.Claims;
using System.Threading.RateLimiting;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public sealed class EventStoreWorkloadRateLimitTests
{
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

    private static DefaultHttpContext Context(IServiceProvider provider, ClaimsPrincipal? principal)
        => new() { RequestServices = provider, User = principal ?? new ClaimsPrincipal() };

    private static ClaimsPrincipal Workload(string caller, string tenant)
        => new(new ClaimsIdentity(
        [
            new Claim(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType, caller),
            new Claim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant)
        ], DaprInternalAuthenticationOptions.SchemeName));
}
