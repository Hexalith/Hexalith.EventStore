
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>
/// Story 4.3 Task 6.2: BackpressureOptions unit tests (AC: #8).
/// </summary>
public class BackpressureOptionsTests {
    [Fact]
    public void DefaultValues_CorrectDefaults() {
        var options = new BackpressureOptions();

        options.MaxPendingCommandsPerAggregate.ShouldBe(100);
        options.RetryAfterSeconds.ShouldBe(10);
    }

    [Fact]
    public void ConfigurationBinding_OverridesDefaults() {
        var configValues = new Dictionary<string, string?> {
            ["EventStore:Backpressure:MaxPendingCommandsPerAggregate"] = "50",
            ["EventStore:Backpressure:RetryAfterSeconds"] = "30",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        _ = services.AddOptions<BackpressureOptions>()
            .Bind(configuration.GetSection("EventStore:Backpressure"));

        ServiceProvider provider = services.BuildServiceProvider();

        BackpressureOptions options = provider.GetRequiredService<IOptions<BackpressureOptions>>().Value;

        options.MaxPendingCommandsPerAggregate.ShouldBe(50);
        options.RetryAfterSeconds.ShouldBe(30);
    }

    [Fact]
    public void Validation_RejectsZeroMaxPending() {
        var validator = new ValidateBackpressureOptions();
        var options = new BackpressureOptions { MaxPendingCommandsPerAggregate = 0 };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxPendingCommandsPerAggregate");
    }

    [Fact]
    public void Validation_RejectsNegativeMaxPending() {
        var validator = new ValidateBackpressureOptions();
        var options = new BackpressureOptions { MaxPendingCommandsPerAggregate = -1 };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validation_RejectsZeroRetryAfter() {
        var validator = new ValidateBackpressureOptions();
        var options = new BackpressureOptions { RetryAfterSeconds = 0 };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RetryAfterSeconds");
    }

    [Fact]
    public void Validation_RejectsNegativeRetryAfter() {
        var validator = new ValidateBackpressureOptions();
        var options = new BackpressureOptions { RetryAfterSeconds = -5 };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validation_AcceptsValidOptions() {
        var validator = new ValidateBackpressureOptions();
        var options = new BackpressureOptions { MaxPendingCommandsPerAggregate = 100, RetryAfterSeconds = 10 };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
