
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;
/// <summary>
/// Story 4.2: EventDrainOptions unit tests (AC: #10).
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

        // Story 4.4 bounds. MaxDrainAttempts mirrors ProjectionDispatchOptions.DefaultMaxRetryAttempts.
        options.MaxDrainAttempts.ShouldBe(8);
        options.MaxDrainAttempts.ShouldBe(EventDrainOptions.DefaultMaxDrainAttempts);

        // Unset by default so the bound tracks the backpressure ceiling. A fixed default above that
        // ceiling would make the fail-closed branch unreachable in production.
        options.MaxOutstandingPublicationEntries
            .ShouldBe(EventDrainOptions.DeriveMaxOutstandingPublicationEntries);
    }

    [Fact]
    public void ConfigurationBinding_OverridesDefaults() {
        // Arrange
        var configValues = new Dictionary<string, string?> {
            ["EventStore:Drain:InitialDrainDelay"] = "00:00:10",
            ["EventStore:Drain:DrainPeriod"] = "00:02:00",
            ["EventStore:Drain:MaxDrainPeriod"] = "01:00:00",
            ["EventStore:Drain:MaxDrainAttempts"] = "3",
            ["EventStore:Drain:MaxOutstandingPublicationEntries"] = "12",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        _ = services.AddOptions<EventDrainOptions>()
            .Bind(configuration.GetSection("EventStore:Drain"));

        ServiceProvider provider = services.BuildServiceProvider();

        // Act
        EventDrainOptions options = provider.GetRequiredService<IOptions<EventDrainOptions>>().Value;

        // Assert
        options.InitialDrainDelay.ShouldBe(TimeSpan.FromSeconds(10));
        options.DrainPeriod.ShouldBe(TimeSpan.FromMinutes(2));
        options.MaxDrainPeriod.ShouldBe(TimeSpan.FromHours(1));
        options.MaxDrainAttempts.ShouldBe(3);
        options.MaxOutstandingPublicationEntries.ShouldBe(12);
    }

    // --- Story 4.4: the new bounds are normalized at the point of use, matching the tolerant
    // policy already applied to the timing fields. There is deliberately no second, startup-time
    // policy over the same inputs. ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NormalizeMaxDrainAttempts_NonPositiveValue_FallsBackToDefault(int configured)
        => EventDrainOptions.NormalizeMaxDrainAttempts(configured)
            .ShouldBe(EventDrainOptions.DefaultMaxDrainAttempts);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(64)]
    public void NormalizeMaxDrainAttempts_PositiveValue_IsPreserved(int configured)
        => EventDrainOptions.NormalizeMaxDrainAttempts(configured).ShouldBe(configured);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NormalizeMaxOutstandingPublicationEntries_UnsetValue_TracksTheBackpressureCeiling(int configured)
        => EventDrainOptions.NormalizeMaxOutstandingPublicationEntries(
            configured,
            maxPendingCommandsPerAggregate: 100).ShouldBe(100);

    [Fact]
    public void NormalizeMaxOutstandingPublicationEntries_UnsetWithInvalidCeiling_StaysStrictlyPositive()
        => EventDrainOptions.NormalizeMaxOutstandingPublicationEntries(
            EventDrainOptions.DeriveMaxOutstandingPublicationEntries,
            maxPendingCommandsPerAggregate: 0).ShouldBe(1);

    [Fact]
    public void NormalizeMaxOutstandingPublicationEntries_DerivedDefault_DoesNotExceedTheBackpressureCeiling() {
        // A bound above the backpressure ceiling could never be reached, because an index entry and
        // a pending slot are created and released together. Parity keeps the fail-closed branch a
        // real backstop for a pending counter that has drifted below the true outstanding count.
        int backpressureCeiling = new BackpressureOptions().MaxPendingCommandsPerAggregate;
        int derived = EventDrainOptions.NormalizeMaxOutstandingPublicationEntries(
            new EventDrainOptions().MaxOutstandingPublicationEntries,
            backpressureCeiling);

        derived.ShouldBeLessThanOrEqualTo(backpressureCeiling);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    public void NormalizeMaxOutstandingPublicationEntries_PositiveValue_IsPreserved(int configured)
        => EventDrainOptions.NormalizeMaxOutstandingPublicationEntries(
            configured,
            maxPendingCommandsPerAggregate: 100).ShouldBe(configured);

    [Fact]
    public void ValidateOnStart_IsNotAppliedToTheTolerantTimingFields() {
        // Guard for the loop-1 regression: a startup policy over InitialDrainDelay / DrainPeriod /
        // MaxDrainPeriod would contradict the documented runtime normalization and make its
        // fallback branches unreachable. Constructing invalid timing values must stay legal.
        var options = new EventDrainOptions {
            InitialDrainDelay = TimeSpan.FromSeconds(-1),
            DrainPeriod = TimeSpan.Zero,
            MaxDrainPeriod = TimeSpan.Zero,
        };

        options.InitialDrainDelay.ShouldBe(TimeSpan.FromSeconds(-1));
        options.DrainPeriod.ShouldBe(TimeSpan.Zero);
        options.MaxDrainPeriod.ShouldBe(TimeSpan.Zero);
        typeof(EventDrainOptions).GetMethod("Validate").ShouldBeNull();
    }
}
