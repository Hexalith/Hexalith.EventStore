using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class ProjectionChangeNotifierOptionsTests {
    private readonly ValidateProjectionChangeNotifierOptions _validator = new();

    [Fact]
    public void DefaultValues_AreCorrect() {
        var options = new ProjectionChangeNotifierOptions();

        options.PubSubName.ShouldBe(ProjectionChangeNotifierOptions.DefaultPubSubName);
        options.Transport.ShouldBe(ProjectionChangeTransport.PubSub);
        options.MaxDetailMetadataEntries.ShouldBe(ProjectionChangeNotifierOptions.DefaultMaxDetailMetadataEntries);
        options.MaxDetailMetadataBytes.ShouldBe(ProjectionChangeNotifierOptions.DefaultMaxDetailMetadataBytes);
    }

    [Fact]
    public void Validation_PubSubTransportWithDefaultComponent_Succeeds() {
        var options = new ProjectionChangeNotifierOptions();

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_PubSubTransportWithCustomComponent_Fails() {
        var options = new ProjectionChangeNotifierOptions {
            PubSubName = "custom-pubsub",
            Transport = ProjectionChangeTransport.PubSub,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(ProjectionChangeNotifierOptions.DefaultPubSubName);
    }

    [Fact]
    public void Validation_DirectTransportWithCustomComponent_Succeeds() {
        var options = new ProjectionChangeNotifierOptions {
            PubSubName = "custom-pubsub",
            Transport = ProjectionChangeTransport.Direct,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validation_UndefinedTransport_Fails() {
        var options = new ProjectionChangeNotifierOptions {
            Transport = (ProjectionChangeTransport)999,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("transport");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validation_InvalidMetadataEntryLimit_Fails(int maxEntries) {
        var options = new ProjectionChangeNotifierOptions {
            MaxDetailMetadataEntries = maxEntries,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDetailMetadataEntries");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validation_InvalidMetadataByteLimit_Fails(int maxBytes) {
        var options = new ProjectionChangeNotifierOptions {
            MaxDetailMetadataBytes = maxBytes,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("MaxDetailMetadataBytes");
    }
}
