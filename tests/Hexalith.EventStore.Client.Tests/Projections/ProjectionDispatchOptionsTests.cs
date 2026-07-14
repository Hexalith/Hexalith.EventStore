using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class ProjectionDispatchOptionsTests {
    [Fact]
    public void Defaults_AreBoundedAndValid() {
        var options = new ProjectionDispatchOptions();

        options.MaxHandlersPerDomain.ShouldBe(32);
        options.MaxOutcomes.ShouldBe(32);
        options.MaxReasonCodeBytes.ShouldBe(128);
        options.MaxOutcomeEnvelopeBytes.ShouldBe(1_048_576);
        options.MaxRetryAttempts.ShouldBe(8);
        options.RetryBaseDelay.ShouldBe(TimeSpan.FromSeconds(1));
        options.RetryMaxDelay.ShouldBe(TimeSpan.FromMinutes(5));
        options.RetryWorkerInterval.ShouldBe(TimeSpan.FromSeconds(1));
        Should.NotThrow(options.Validate);
    }

    public static TheoryData<Action<ProjectionDispatchOptions>> NonPositiveLimits => new() {
        options => options.MaxHandlersPerDomain = 0,
        options => options.MaxOutcomes = 0,
        options => options.MaxReasonCodeBytes = 0,
        options => options.MaxOutcomeEnvelopeBytes = 0,
        options => options.MaxRetryAttempts = 0,
        options => options.RetryBaseDelay = TimeSpan.Zero,
        options => options.RetryMaxDelay = TimeSpan.Zero,
        options => options.RetryWorkerInterval = TimeSpan.Zero,
        options => options.RetryLeaseDuration = TimeSpan.Zero,
        options => options.CatalogRefreshInterval = TimeSpan.Zero,
    };

    [Theory]
    [MemberData(nameof(NonPositiveLimits))]
    public void Validate_RejectsNonPositiveLimits(Action<ProjectionDispatchOptions> mutate) {
        ArgumentNullException.ThrowIfNull(mutate);
        var options = new ProjectionDispatchOptions();
        mutate(options);

        _ = Should.Throw<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsFewerOutcomesThanHandlers() {
        var options = new ProjectionDispatchOptions {
            MaxHandlersPerDomain = 4,
            MaxOutcomes = 3,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsRetryMaximumBelowBaseDelay() {
        var options = new ProjectionDispatchOptions {
            RetryBaseDelay = TimeSpan.FromSeconds(2),
            RetryMaxDelay = TimeSpan.FromSeconds(1),
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_RejectsEnvelopeThatCannotRepresentEverySafeOutcome() {
        var options = new ProjectionDispatchOptions { MaxOutcomeEnvelopeBytes = 128 };

        _ = Should.Throw<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void Validate_RequiresExplicitQuiescedWriterMarkerForLegacyMigration() {
        var options = new ProjectionDispatchOptions { EnableLegacyRetryLedgerMigration = true };

        _ = Should.Throw<ArgumentException>(options.Validate);
    }
}
