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
}