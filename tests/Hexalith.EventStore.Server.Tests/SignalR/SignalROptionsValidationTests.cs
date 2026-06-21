using Hexalith.EventStore.SignalRHub;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.SignalR;

public class SignalROptionsValidationTests {
    private readonly ValidateSignalROptions _validator = new();

    [Fact]
    public void DefaultValues_AreCorrect() {
        var options = new SignalROptions();

        options.Enabled.ShouldBeFalse();
        options.MaxGroupsPerConnection.ShouldBe(50);
        options.BackplaneRedisConnectionString.ShouldBeNull();
        options.MaxDetailMetadataEntries.ShouldBe(16);
        options.MaxDetailMetadataBytes.ShouldBe(2048);
    }

    [Fact]
    public void Validation_WithNonPositiveMetadataEntryCap_Fails() {
        var options = new SignalROptions {
            MaxDetailMetadataEntries = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDetailMetadataEntries");
    }

    [Fact]
    public void Validation_WithNonPositiveMetadataByteCap_Fails() {
        var options = new SignalROptions {
            MaxDetailMetadataBytes = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDetailMetadataBytes");
    }

    [Fact]
    public void Validation_WithPositiveMetadataBounds_Succeeds() {
        var options = new SignalROptions {
            Enabled = true,
            MaxDetailMetadataEntries = 8,
            MaxDetailMetadataBytes = 1024,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_WithPositiveGroupLimit_Succeeds() {
        var options = new SignalROptions {
            Enabled = true,
            MaxGroupsPerConnection = 5,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_WithNonPositiveGroupLimit_Fails() {
        var options = new SignalROptions {
            MaxGroupsPerConnection = 0,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxGroupsPerConnection");
    }

    [Fact]
    public void Validation_WithWhitespaceBackplaneConnectionString_Fails() {
        var options = new SignalROptions {
            BackplaneRedisConnectionString = "   ",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("BackplaneRedisConnectionString");
    }
}