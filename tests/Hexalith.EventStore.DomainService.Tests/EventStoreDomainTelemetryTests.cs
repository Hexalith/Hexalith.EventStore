using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

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

        diagnostics.Domain.ShouldBe("tenants");
        diagnostics.ActivitySource.Name.ShouldBe("Hexalith.EventStore.Domain.tenants");
        diagnostics.Meter.Name.ShouldBe("Hexalith.EventStore.Domain.tenants");
    }

    [Fact]
    public void AddEventStoreDomainStateStoreHealthCheck_RegistersCheckWithConventionalName() {
        var services = new ServiceCollection();

        _ = services.AddHealthChecks().AddEventStoreDomainStateStoreHealthCheck("tenants");

        using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckServiceOptions options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        options.Registrations.ShouldContain(r => r.Name == "dapr-statestore-tenants");
    }
}
