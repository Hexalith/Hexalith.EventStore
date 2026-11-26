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
    [InlineData("src/Hexalith.EventStore.AppHost/DaprComponents")]
    [InlineData("deploy/dapr")]
    [InlineData("samples/dapr-components/redis")]
    [InlineData("samples/dapr-components/postgresql")]
    public void PubSubTopologyDeclaresNoBrokerExpiryOrCaptureBypass(string componentDirectory)
    {
        // Application attempt and age limits decide exhaustion. Broker expiry, overflow, delivery-count dead-lettering
        // or a component dead-letter route could remove an unacknowledged event before Operations confirms capture.
        string[] forbidden = ["deadLetterTopic", "ttlInSeconds", "maxLen", "maxLenBytes", "defaultMessageTimeToLiveInSec", "maxDeliveryCount"];
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        int pubSubComponents = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(repositoryRoot, componentDirectory), "*.yaml", SearchOption.TopDirectoryOnly))
        {
            var yaml = new YamlStream();
            using (StreamReader reader = File.OpenText(path))
            {
                yaml.Load(reader);
            }

            foreach (YamlDocument document in yaml.Documents)
            {
                if (document.RootNode is not YamlMappingNode resource
                    || !resource.Children.TryGetValue("spec", out YamlNode? specNode) || specNode is not YamlMappingNode spec)
                {
                    continue;
                }

                string? kind = resource.Children.TryGetValue("kind", out YamlNode? kindNode) ? kindNode.ToString() : null;
                if (kind == "Component" && spec.Children.TryGetValue("type", out YamlNode? type)
                    && type.ToString().StartsWith("pubsub.", StringComparison.Ordinal))
                {
                    pubSubComponents++;
                    string[] names = spec.Children.TryGetValue("metadata", out YamlNode? metadata) && metadata is YamlSequenceNode items
                        ? [.. items.OfType<YamlMappingNode>().Select(static item => item.Children["name"].ToString())]
                        : [];
                    names.Intersect(forbidden, StringComparer.Ordinal).ShouldBeEmpty(path);
                }
                else if (kind == "Subscription")
                {
                    spec.Children.ContainsKey("deadLetterTopic").ShouldBeFalse(path);
                    if (spec.Children.TryGetValue("metadata", out YamlNode? values) && values is YamlMappingNode subscriptionMetadata)
                    {
                        subscriptionMetadata.Children.ContainsKey("ttlInSeconds").ShouldBeFalse(path);
                    }
                }
            }
        }

        pubSubComponents.ShouldBeGreaterThan(0, componentDirectory);
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
