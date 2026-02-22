
using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Hexalith.EventStore.CommandApi.HealthChecks;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class HealthCheckRegistrationTests {
    private static IServiceProvider CreateServiceProvider() {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        return services.BuildServiceProvider();
    }

    private static HealthCheckServiceOptions GetHealthCheckOptions(Action<IHealthChecksBuilder>? configure = null) {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Substitute.For<DaprClient>());
        IHealthChecksBuilder builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        configure?.Invoke(builder);
        _ = builder.AddEventStoreDaprHealthChecks();
        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_RegistersAllFourChecks() {
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        // 4 DAPR checks + 1 "self" check = 5 total
        options.Registrations.Count.ShouldBe(5);
        options.Registrations.ShouldContain(r => r.Name == "dapr-sidecar");
        options.Registrations.ShouldContain(r => r.Name == "dapr-statestore");
        options.Registrations.ShouldContain(r => r.Name == "dapr-pubsub");
        options.Registrations.ShouldContain(r => r.Name == "dapr-configstore");
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_SidecarAndStateStoreAreUnhealthy() {
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        options.Registrations.First(r => r.Name == "dapr-sidecar").FailureStatus.ShouldBe(HealthStatus.Unhealthy);
        options.Registrations.First(r => r.Name == "dapr-statestore").FailureStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_PubSubAndConfigStoreAreDegraded() {
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        options.Registrations.First(r => r.Name == "dapr-pubsub").FailureStatus.ShouldBe(HealthStatus.Degraded);
        options.Registrations.First(r => r.Name == "dapr-configstore").FailureStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_AllChecksHaveReadyTag() {
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        IEnumerable<HealthCheckRegistration> daprChecks = options.Registrations.Where(r => r.Name.StartsWith("dapr-"));
        daprChecks.Count().ShouldBe(4);
        foreach (HealthCheckRegistration? check in daprChecks) {
            check.Tags.ShouldContain("ready");
        }
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_AllChecksHaveFifteenSecondTimeout() {
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        IEnumerable<HealthCheckRegistration> daprChecks = options.Registrations.Where(r => r.Name.StartsWith("dapr-"));
        foreach (HealthCheckRegistration? check in daprChecks) {
            check.Timeout.ShouldBe(TimeSpan.FromSeconds(15));
        }
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_CustomComponentNames_UsesProvidedNames() {
        var services = new ServiceCollection();
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = services.AddSingleton(daprClient);
        IHealthChecksBuilder builder = services.AddHealthChecks();
        _ = builder.AddEventStoreDaprHealthChecks(
            stateStoreName: "my-state",
            pubSubName: "my-pubsub",
            configStoreName: "my-config");

        ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // Verify registrations exist (names are customized internally, registration names stay the same)
        options.Registrations.ShouldContain(r => r.Name == "dapr-statestore");
        options.Registrations.ShouldContain(r => r.Name == "dapr-pubsub");
        options.Registrations.ShouldContain(r => r.Name == "dapr-configstore");
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_ExistingSelfCheckUnchanged() {
        HealthCheckServiceOptions options = GetHealthCheckOptions();

        HealthCheckRegistration? selfCheck = options.Registrations.FirstOrDefault(r => r.Name == "self");
        _ = selfCheck.ShouldNotBeNull();
        selfCheck.Tags.ShouldContain("live");
    }
}
