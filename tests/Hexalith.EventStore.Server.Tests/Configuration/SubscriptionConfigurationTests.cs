using Shouldly;

using YamlDotNet.RepresentationModel;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>Validates the declared sample subscription and its acknowledgement boundary.</summary>
public sealed class SubscriptionConfigurationTests
{
    [Theory]
    [InlineData("src/Hexalith.EventStore.AppHost/DaprComponents")]
    [InlineData("deploy/dapr")]
    public void SampleSubscriptionUsesStructuredCloudEventsWithoutBrokerExpiryOrDeadLetterForwarding(string componentDirectory)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        using var reader = File.OpenText(Path.Combine(root, componentDirectory, "subscription-sample-counter.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);

        var document = (YamlMappingNode)yaml.Documents.Single().RootNode;
        var spec = (YamlMappingNode)document.Children["spec"];
        document.Children["kind"].ToString().ShouldBe("Subscription");
        spec.Children["pubsubname"].ToString().ShouldBe("pubsub");
        spec.Children["topic"].ToString().ShouldBe("sample.counter.events");
        ((YamlMappingNode)spec.Children["routes"]).Children["default"].ToString().ShouldBe("/events/idempotency-demo");
        var metadata = (YamlMappingNode)spec.Children["metadata"];
        metadata.Children["rawPayload"].ToString().ShouldBe("false");
        metadata.Children.ContainsKey("ttlInSeconds").ShouldBeFalse();
        spec.Children.ContainsKey("deadLetterTopic").ShouldBeFalse();
    }

    [Theory]
    [InlineData("src/Hexalith.EventStore.AppHost/DaprComponents", "30s")]
    [InlineData("deploy/dapr", "60s")]
    public void PubSubTransportKeepsUnretainedDeliveryRetryableWithBackoff(string componentDirectory, string maxInterval)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        using var reader = File.OpenText(Path.Combine(root, componentDirectory, "resiliency.yaml"));
        var yaml = new YamlStream();
        yaml.Load(reader);

        YamlNode spec = yaml.Documents.Single().RootNode["spec"];
        YamlNode inbound = spec["targets"]["components"]["pubsub"]["inbound"];
        YamlNode policy = spec["policies"]["retries"][inbound["retry"].ToString()];
        policy["policy"].ToString().ShouldBe("exponential");
        policy["maxInterval"].ToString().ShouldBe(maxInterval);
        policy["maxRetries"].ToString().ShouldBe("-1");
    }
}
