namespace Hexalith.EventStore.Server.Tests.Configuration;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

/// <summary>
/// Story 4.4 Task 11: EventDrainOptions unit tests (AC: #11).
/// </summary>
public class EventDrainOptionsTests {
    [Fact]
    public void DefaultValues_CorrectDefaults() {
        // Act
        var options = new EventDrainOptions();

        // Assert
        options.InitialDrainDelay.ShouldBe(TimeSpan.FromSeconds(30));
        options.DrainPeriod.ShouldBe(TimeSpan.FromMinutes(1));
        options.MaxDrainPeriod.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void ConfigurationBinding_OverridesDefaults() {
        // Arrange
        var configValues = new Dictionary<string, string?> {
            ["EventStore:Drain:InitialDrainDelay"] = "00:00:10",
            ["EventStore:Drain:DrainPeriod"] = "00:02:00",
            ["EventStore:Drain:MaxDrainPeriod"] = "01:00:00",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<EventDrainOptions>()
            .Bind(configuration.GetSection("EventStore:Drain"));

        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        EventDrainOptions options = provider.GetRequiredService<IOptions<EventDrainOptions>>().Value;

        // Assert
        options.InitialDrainDelay.ShouldBe(TimeSpan.FromSeconds(10));
        options.DrainPeriod.ShouldBe(TimeSpan.FromMinutes(2));
        options.MaxDrainPeriod.ShouldBe(TimeSpan.FromHours(1));
    }
}
