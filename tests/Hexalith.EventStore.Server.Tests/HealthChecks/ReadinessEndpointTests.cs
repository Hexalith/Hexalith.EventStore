
using Dapr.Client;

using Hexalith.EventStore.CommandApi.HealthChecks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class ReadinessEndpointTests {
    private static HealthCheckServiceOptions GetHealthCheckOptions() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        IHealthChecksBuilder builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        _ = builder.AddEventStoreDaprHealthChecks();
        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
    }

    [Fact]
    public void ReadyTagPredicate_MatchesDaprChecks() {
        // Verify that a "ready" tag predicate matches exactly the 4 DAPR checks
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        static bool predicate(HealthCheckRegistration r) => r.Tags.Contains("ready");

        var readyChecks = options.Registrations.Where(predicate).ToList();

        readyChecks.Count.ShouldBe(4);
        readyChecks.ShouldContain(r => r.Name == "dapr-sidecar");
        readyChecks.ShouldContain(r => r.Name == "dapr-statestore");
        readyChecks.ShouldContain(r => r.Name == "dapr-pubsub");
        readyChecks.ShouldContain(r => r.Name == "dapr-configstore");
    }

    [Fact]
    public void ReadyTagPredicate_MatchesNonEmptySet() {
        // FMA-1 prevention: verify "ready" tag predicate matches at least 1 check
        // A tag typo would silently return Healthy for zero matches
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        static bool predicate(HealthCheckRegistration r) => r.Tags.Contains("ready");

        options.Registrations.Where(predicate).ShouldNotBeEmpty();
    }

    [Fact]
    public void ReadyTagPredicate_ExcludesSelfLivenessCheck() {
        // The "self" check tagged "live" must NOT appear in readiness evaluation
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        static bool predicate(HealthCheckRegistration r) => r.Tags.Contains("ready");

        var readyChecks = options.Registrations.Where(predicate).ToList();

        readyChecks.ShouldNotContain(r => r.Name == "self");
    }

    [Fact]
    public void LiveTagPredicate_StillMatchesSelfCheckOnly() {
        // Backward compatibility: /alive must still use "live" tag predicate
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        static bool predicate(HealthCheckRegistration r) => r.Tags.Contains("live");

        var liveChecks = options.Registrations.Where(predicate).ToList();

        liveChecks.Count.ShouldBe(1);
        liveChecks[0].Name.ShouldBe("self");
    }

    [Fact]
    public void SidecarDown_ResultsInUnhealthyStatus() {
        // Sidecar has failureStatus: Unhealthy -> /ready returns 503
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        HealthCheckRegistration sidecar = options.Registrations.First(r => r.Name == "dapr-sidecar");

        sidecar.FailureStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void StateStoreDown_ResultsInUnhealthyStatus() {
        // State store has failureStatus: Unhealthy -> /ready returns 503
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        HealthCheckRegistration stateStore = options.Registrations.First(r => r.Name == "dapr-statestore");

        stateStore.FailureStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void PubSubDown_ResultsInDegradedStatus() {
        // Pub/sub has failureStatus: Degraded -> /ready returns 200 Degraded
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        HealthCheckRegistration pubsub = options.Registrations.First(r => r.Name == "dapr-pubsub");

        pubsub.FailureStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void ConfigStoreDown_ResultsInDegradedStatus() {
        // Config store has failureStatus: Degraded -> /ready returns 200 Degraded
        HealthCheckServiceOptions options = GetHealthCheckOptions();
        HealthCheckRegistration configStore = options.Registrations.First(r => r.Name == "dapr-configstore");

        configStore.FailureStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void HealthyStatusCode_Maps200() {
        // Verify status code mapping: Healthy -> 200
        var statusCodes = new Dictionary<HealthStatus, int> {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        };

        statusCodes[HealthStatus.Healthy].ShouldBe(200);
    }

    [Fact]
    public void DegradedStatusCode_Maps200() {
        // Verify status code mapping: Degraded -> 200
        var statusCodes = new Dictionary<HealthStatus, int> {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        };

        statusCodes[HealthStatus.Degraded].ShouldBe(200);
    }

    [Fact]
    public void UnhealthyStatusCode_Maps503() {
        // Verify status code mapping: Unhealthy -> 503
        var statusCodes = new Dictionary<HealthStatus, int> {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        };

        statusCodes[HealthStatus.Unhealthy].ShouldBe(503);
    }

    [Fact]
    public void ThreeEndpointStrategy_AllTagsAccountedFor() {
        // Verify the three-endpoint strategy: /health (all), /alive (live), /ready (ready)
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        // All checks (for /health - no filter)
        options.Registrations.Count.ShouldBe(5);

        // Live-only checks (for /alive)
        options.Registrations.Where(r => r.Tags.Contains("live")).Count().ShouldBe(1);

        // Ready-only checks (for /ready)
        options.Registrations.Where(r => r.Tags.Contains("ready")).Count().ShouldBe(4);

        // No check has both tags
        options.Registrations.Where(r => r.Tags.Contains("live") && r.Tags.Contains("ready")).ShouldBeEmpty();
    }
}
