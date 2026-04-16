using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprSubscriptionInfoTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var info = new DaprSubscriptionInfo("pubsub", "*.*.events", "/events/handle", "DECLARATIVE", "deadletter.topic");

        info.PubSubName.ShouldBe("pubsub");
        info.Topic.ShouldBe("*.*.events");
        info.Route.ShouldBe("/events/handle");
        info.SubscriptionType.ShouldBe("DECLARATIVE");
        info.DeadLetterTopic.ShouldBe("deadletter.topic");
    }

    [Fact]
    public void Constructor_WithNullDeadLetterTopic_CreatesInstance() {
        var info = new DaprSubscriptionInfo("pubsub", "*.*.events", "/events/handle", "PROGRAMMATIC", null);

        info.DeadLetterTopic.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyDeadLetterTopic_CreatesInstance() {
        var info = new DaprSubscriptionInfo("pubsub", "*.*.events", "/events/handle", "DECLARATIVE", "");

        info.DeadLetterTopic.ShouldBe("");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidPubSubName_ThrowsArgumentException(string? pubSubName) => Should.Throw<ArgumentException>(() =>
                                                                                                          new DaprSubscriptionInfo(pubSubName!, "topic", "/route", "DECLARATIVE", null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTopic_ThrowsArgumentException(string? topic) => Should.Throw<ArgumentException>(() =>
                                                                                                new DaprSubscriptionInfo("pubsub", topic!, "/route", "DECLARATIVE", null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidRoute_ThrowsArgumentException(string? route) => Should.Throw<ArgumentException>(() =>
                                                                                                new DaprSubscriptionInfo("pubsub", "topic", route!, "DECLARATIVE", null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidSubscriptionType_ThrowsArgumentException(string? subscriptionType) => Should.Throw<ArgumentException>(() =>
                                                                                                                      new DaprSubscriptionInfo("pubsub", "topic", "/route", subscriptionType!, null));
}
