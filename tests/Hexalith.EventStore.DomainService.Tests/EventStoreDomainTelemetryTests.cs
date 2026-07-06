using Dapr.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>
/// Tier 1 coverage of the convention-driven telemetry and DAPR state-store health check (Epic A5).
/// </summary>
public sealed class EventStoreDomainTelemetryTests {
    [Fact]
    public void ActivitySourceName_AppendsDomainToPrefix() =>
        EventStoreDomainTelemetry.ActivitySourceName("counter").ShouldBe("Hexalith.EventStore.Domain.counter");

    [Fact]
    public void ActivitySourceName_TrimsDomain() =>
        EventStoreDomainTelemetry.ActivitySourceName("  counter  ").ShouldBe("Hexalith.EventStore.Domain.counter");

    [Fact]
    public void MeterName_MatchesActivitySourceName() =>
        EventStoreDomainTelemetry.MeterName("tenants").ShouldBe(EventStoreDomainTelemetry.ActivitySourceName("tenants"));

    [Fact]
    public void StateStoreHealthCheckName_UsesConventionalPrefix() =>
        EventStoreDomainTelemetry.StateStoreHealthCheckName("tenants").ShouldBe("dapr-statestore-tenants");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ActivitySourceName_Throws_WhenDomainMissing(string? domain) =>
        _ = Should.Throw<ArgumentException>(() => EventStoreDomainTelemetry.ActivitySourceName(domain!));

    [Fact]
    public void AddEventStoreDomainTelemetry_RegistersConventionNamedDiagnostics() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.AddEventStoreDomainTelemetry("tenants");

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        EventStoreDomainDiagnostics diagnostics = provider.GetRequiredService<EventStoreDomainDiagnostics>();
        EventStoreDomainDiagnosticsRegistry registry = provider.GetRequiredService<EventStoreDomainDiagnosticsRegistry>();

        diagnostics.Domain.ShouldBe("tenants");
        diagnostics.ActivitySource.Name.ShouldBe("Hexalith.EventStore.Domain.tenants");
        diagnostics.Meter.Name.ShouldBe("Hexalith.EventStore.Domain.tenants");
        registry.GetDiagnostics(" tenants ").ShouldBeSameAs(diagnostics);
    }

    [Fact]
    public void AddEventStoreDomainTelemetry_RegistersEachDomainOnce() {
        var services = new ServiceCollection();

        _ = services.AddEventStoreDomainTelemetry(["counter", " counter ", "greeting"]);

        using ServiceProvider provider = services.BuildServiceProvider();

        EventStoreDomainDiagnosticsRegistry registry = provider.GetRequiredService<EventStoreDomainDiagnosticsRegistry>();
        registry.Domains.OrderBy(static domain => domain, StringComparer.Ordinal).ShouldBe(["counter", "greeting"]);
        provider.GetRequiredKeyedService<EventStoreDomainDiagnostics>("counter").ShouldBeSameAs(registry.GetDiagnostics("counter"));
    }

    [Fact]
    public void DirectEventStoreDomainDiagnosticsInjection_ThrowsWhenMultipleDomainsAreRegistered() {
        var services = new ServiceCollection();
        _ = services.AddEventStoreDomainTelemetry(["counter", "greeting"]);
        using ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            provider.GetRequiredService<EventStoreDomainDiagnostics>);

        ex.Message.ShouldContain("exactly one domain");
    }

    [Fact]
    public void EventStoreDomainDiagnosticsRegistry_ReturnsNullForUnknownDomain() {
        var services = new ServiceCollection();
        _ = services.AddEventStoreDomainTelemetry(["counter", "greeting"]);
        using ServiceProvider provider = services.BuildServiceProvider();

        EventStoreDomainDiagnosticsRegistry registry = provider.GetRequiredService<EventStoreDomainDiagnosticsRegistry>();

        registry.GetDiagnostics("unknown").ShouldBeNull();
    }

    [Fact]
    public void AddEventStoreDomainStateStoreHealthCheck_RegistersCheckWithConventionalName() {
        var services = new ServiceCollection();

        _ = services.AddHealthChecks().AddEventStoreDomainStateStoreHealthCheck("tenants");

        using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckServiceOptions options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        options.Registrations.ShouldContain(r => r.Name == "dapr-statestore-tenants");
    }

    [Fact]
    public async Task DaprStateStoreHealthCheck_WhenProbeSucceeds_ReturnsHealthy() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>("statestore", "health-probe", cancellationToken: Arg.Any<CancellationToken>())
            .Returns((string)null!);
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, "statestore", "health-probe");

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task DaprStateStoreHealthCheck_WhenProbeFails_ReturnsUnhealthyWithoutLeakingException() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<string>("statestore", "health-probe", cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("secret payload value"));
        var healthCheck = new DaprStateStoreHealthCheck(daprClient, "statestore", "health-probe");

        HealthCheckResult result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("DAPR state store 'statestore' is unreachable");
        result.Description.ShouldNotContain("secret payload value");
    }
}
