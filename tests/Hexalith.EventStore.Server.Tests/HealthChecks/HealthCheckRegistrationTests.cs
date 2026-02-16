namespace Hexalith.EventStore.Server.Tests.HealthChecks;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

public class HealthCheckRegistrationTests
{
    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<DaprClient>());
        return services.BuildServiceProvider();
    }

    private static HealthCheckServiceOptions GetHealthCheckOptions(Action<IHealthChecksBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<DaprClient>());
        var builder = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        configure?.Invoke(builder);
        builder.AddEventStoreDaprHealthChecks();
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_RegistersAllFourChecks()
    {
        var options = GetHealthCheckOptions();

        // 4 DAPR checks + 1 "self" check = 5 total
        options.Registrations.Count.ShouldBe(5);
        options.Registrations.ShouldContain(r => r.Name == "dapr-sidecar");
        options.Registrations.ShouldContain(r => r.Name == "dapr-statestore");
        options.Registrations.ShouldContain(r => r.Name == "dapr-pubsub");
        options.Registrations.ShouldContain(r => r.Name == "dapr-configstore");
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_SidecarAndStateStoreAreUnhealthy()
    {
        var options = GetHealthCheckOptions();

        options.Registrations.First(r => r.Name == "dapr-sidecar").FailureStatus.ShouldBe(HealthStatus.Unhealthy);
        options.Registrations.First(r => r.Name == "dapr-statestore").FailureStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_PubSubAndConfigStoreAreDegraded()
    {
        var options = GetHealthCheckOptions();

        options.Registrations.First(r => r.Name == "dapr-pubsub").FailureStatus.ShouldBe(HealthStatus.Degraded);
        options.Registrations.First(r => r.Name == "dapr-configstore").FailureStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_AllChecksHaveReadyTag()
    {
        var options = GetHealthCheckOptions();

        var daprChecks = options.Registrations.Where(r => r.Name.StartsWith("dapr-"));
        daprChecks.Count().ShouldBe(4);
        foreach (var check in daprChecks)
        {
            check.Tags.ShouldContain("ready");
        }
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_AllChecksHaveThreeSecondTimeout()
    {
        var options = GetHealthCheckOptions();

        var daprChecks = options.Registrations.Where(r => r.Name.StartsWith("dapr-"));
        foreach (var check in daprChecks)
        {
            check.Timeout.ShouldBe(TimeSpan.FromSeconds(3));
        }
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_CustomComponentNames_UsesProvidedNames()
    {
        var services = new ServiceCollection();
        var daprClient = Substitute.For<DaprClient>();
        services.AddSingleton(daprClient);
        var builder = services.AddHealthChecks();
        builder.AddEventStoreDaprHealthChecks(
            stateStoreName: "my-state",
            pubSubName: "my-pubsub",
            configStoreName: "my-config");

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        // Verify registrations exist (names are customized internally, registration names stay the same)
        options.Registrations.ShouldContain(r => r.Name == "dapr-statestore");
        options.Registrations.ShouldContain(r => r.Name == "dapr-pubsub");
        options.Registrations.ShouldContain(r => r.Name == "dapr-configstore");
    }

    [Fact]
    public void AddEventStoreDaprHealthChecks_ExistingSelfCheckUnchanged()
    {
        var options = GetHealthCheckOptions();

        var selfCheck = options.Registrations.FirstOrDefault(r => r.Name == "self");
        selfCheck.ShouldNotBeNull();
        selfCheck.Tags.ShouldContain("live");
    }
}
