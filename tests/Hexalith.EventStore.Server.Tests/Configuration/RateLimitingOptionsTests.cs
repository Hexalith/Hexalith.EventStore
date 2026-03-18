
using Hexalith.EventStore.CommandApi.Configuration;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class RateLimitingOptionsTests {
    [Fact]
    public void DefaultValues_AreCorrect() {
        // Arrange & Act
        var options = new RateLimitingOptions();

        // Assert
        options.PermitLimit.ShouldBe(1000);
        options.WindowSeconds.ShouldBe(60);
        options.SegmentsPerWindow.ShouldBe(6);
        options.QueueLimit.ShouldBe(0);
    }

    [Fact]
    public void Validation_PermitLimitZero_Fails() {
        // Arrange
        var options = new RateLimitingOptions { PermitLimit = 0 };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("PermitLimit");
    }

    [Fact]
    public void Validation_WindowSecondsZero_Fails() {
        // Arrange
        var options = new RateLimitingOptions { WindowSeconds = 0 };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("WindowSeconds");
    }

    [Fact]
    public void Validation_QueueLimitNegative_Fails() {
        // Arrange
        var options = new RateLimitingOptions { QueueLimit = -1 };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("QueueLimit");
    }

    [Fact]
    public void Validation_ValidConfiguration_Succeeds() {
        // Arrange
        var options = new RateLimitingOptions {
            PermitLimit = 50,
            WindowSeconds = 30,
            SegmentsPerWindow = 3,
            QueueLimit = 5,
        };
        var validator = new ValidateRateLimitingOptions();

        // Act
        ValidateOptionsResult result = validator.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }
}
