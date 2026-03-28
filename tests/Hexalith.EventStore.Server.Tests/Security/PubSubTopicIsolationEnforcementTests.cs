
using Shouldly;

using YamlDotNet.Serialization;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Story 5.3: Pub/Sub topic isolation enforcement tests (AC #1, #2, #5, #6, #9).
/// Validates subscriber-side scoping in all pub/sub YAML configurations:
/// subscription scoping metadata, publisher scoping regression, dead-letter separation,
/// local-production consistency, and domain service zero-access enforcement.
/// Uses YamlDotNet for YAML parsing (matches AccessControlPolicyTests pattern).
/// </summary>
public class PubSubTopicIsolationEnforcementTests {
    private static readonly IDeserializer YamlParser = new DeserializerBuilder().Build();

    private static readonly string LocalPubSubPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "pubsub.yaml"));

    private static readonly string ProductionRabbitMqPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "pubsub-rabbitmq.yaml"));

    private static readonly string ProductionKafkaPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "pubsub-kafka.yaml"));

    // --- Task 3.2: AC #1, #2, #9 ---

    [Fact]
    public void LocalPubSubYaml_HasSubscriptionScoping_RestrictsSubscribers() {
        Dictionary<string, object> doc = LoadYaml(LocalPubSubPath);

        // subscriptionScopes metadata must exist
        string? subscriptionScopes = GetComponentMetadataValue(doc, "subscriptionScopes");
        _ = subscriptionScopes.ShouldNotBeNull(
            "Local pub/sub must have subscriptionScopes metadata for subscriber-side isolation (FR29)");

        // sample must be explicitly denied subscription access
        subscriptionScopes!.Contains("sample=").ShouldBeTrue(
            "Local pub/sub subscriptionScopes must explicitly deny sample subscription access");
    }

    // --- Task 3.3: AC #5 (regression) ---

    [Fact]
    public void LocalPubSubYaml_PublishingScopes_OnlyEventStoreCanPublish() {
        Dictionary<string, object> doc = LoadYaml(LocalPubSubPath);

        string? publishingScopes = GetComponentMetadataValue(doc, "publishingScopes");
        _ = publishingScopes.ShouldNotBeNull(
            "Local pub/sub must have publishingScopes for publisher-side isolation (Story 5.1)");

        // sample must be explicitly denied publishing access
        publishingScopes!.Contains("sample=").ShouldBeTrue(
            "Local pub/sub publishingScopes must explicitly deny sample publishing access (Story 5.1 regression)");

        // eventstore must NOT appear in publishingScopes (unlisted = unrestricted, NFR20)
        ShouldNotContainAppId(publishingScopes, "eventstore",
            "eventstore must NOT be listed in publishingScopes -- unlisted means unrestricted access (NFR20). " +
            "DAPR does not support wildcards; 'eventstore=*' would restrict to literal topic '*'.");
    }

    // --- Task 3.4: AC #6 ---

    [Fact]
    public void LocalPubSubYaml_DeadLetterTopics_SeparateSubscriptionScope() {
        Dictionary<string, object> doc = LoadYaml(LocalPubSubPath);

        // Dead-letter must be enabled
        GetComponentMetadataValue(doc, "enableDeadLetter").ShouldBe("true",
            "Local pub/sub must have dead-letter enabled");
        _ = GetComponentMetadataValue(doc, "deadLetterTopic").ShouldNotBeNull(
            "Local pub/sub must configure a dead-letter topic");

        // subscriptionScopes must exist (dead-letter topic scoping is controlled by the same field)
        string? subscriptionScopes = GetComponentMetadataValue(doc, "subscriptionScopes");
        _ = subscriptionScopes.ShouldNotBeNull(
            "Local pub/sub must have subscriptionScopes for dead-letter topic isolation (AC #6)");

        // Verify that sample is denied all subscription access (including dead-letter topics)
        subscriptionScopes!.Contains("sample=").ShouldBeTrue(
            "sample must be denied ALL subscription access including dead-letter topics");

        // eventstore must NOT be in subscriptionScopes (unrestricted access to both regular and dead-letter)
        ShouldNotContainAppId(subscriptionScopes, "eventstore",
            "eventstore must NOT be listed in subscriptionScopes -- needs unrestricted access " +
            "to both regular and dead-letter topics");
    }

    // --- Task 3.5: AC #9 ---

    [Fact]
    public void ProductionRabbitMqYaml_HasSubscriptionScoping_RestrictsSubscribers() {
        Dictionary<string, object> doc = LoadYaml(ProductionRabbitMqPath);

        // subscriptionScopes metadata must exist
        string? subscriptionScopes = GetComponentMetadataValue(doc, "subscriptionScopes");
        _ = subscriptionScopes.ShouldNotBeNull(
            "Production RabbitMQ pub/sub must have subscriptionScopes metadata (FR29)");
        subscriptionScopes.ShouldNotBeEmpty(
            "Production RabbitMQ pub/sub subscriptionScopes must be non-empty to enforce subscriber isolation");

        // eventstore must NOT appear in subscriptionScopes (unlisted = unrestricted)
        ShouldNotContainAppId(subscriptionScopes!, "eventstore",
            "Production RabbitMQ: eventstore must NOT be listed in subscriptionScopes -- " +
            "unlisted means unrestricted access. DAPR does not support wildcards.");

        // publishingScopes must also exist and not restrict eventstore
        string? publishingScopes = GetComponentMetadataValue(doc, "publishingScopes");
        _ = publishingScopes.ShouldNotBeNull(
            "Production RabbitMQ pub/sub must have publishingScopes metadata");
        ShouldNotContainAppId(publishingScopes!, "eventstore",
            "Production RabbitMQ: eventstore must NOT be listed in publishingScopes (NFR20)");
    }

    // --- Task 3.6: AC #9 ---

    [Fact]
    public void ProductionKafkaYaml_HasSubscriptionScoping_RestrictsSubscribers() {
        Dictionary<string, object> doc = LoadYaml(ProductionKafkaPath);

        // subscriptionScopes metadata must exist
        string? subscriptionScopes = GetComponentMetadataValue(doc, "subscriptionScopes");
        _ = subscriptionScopes.ShouldNotBeNull(
            "Production Kafka pub/sub must have subscriptionScopes metadata (FR29)");
        subscriptionScopes.ShouldNotBeEmpty(
            "Production Kafka pub/sub subscriptionScopes must be non-empty to enforce subscriber isolation");

        // eventstore must NOT appear in subscriptionScopes (unlisted = unrestricted)
        ShouldNotContainAppId(subscriptionScopes!, "eventstore",
            "Production Kafka: eventstore must NOT be listed in subscriptionScopes -- " +
            "unlisted means unrestricted access. DAPR does not support wildcards.");

        // publishingScopes must also exist and not restrict eventstore
        string? publishingScopes = GetComponentMetadataValue(doc, "publishingScopes");
        _ = publishingScopes.ShouldNotBeNull(
            "Production Kafka pub/sub must have publishingScopes metadata");
        ShouldNotContainAppId(publishingScopes!, "eventstore",
            "Production Kafka: eventstore must NOT be listed in publishingScopes (NFR20)");
    }

    // --- Task 3.7: AC #8, #9 ---

    [Fact]
    public void AllPubSubConfigs_SubscriptionScopingTopology_Consistent() {
        Dictionary<string, object> localDoc = LoadYaml(LocalPubSubPath);
        Dictionary<string, object> rabbitDoc = LoadYaml(ProductionRabbitMqPath);
        Dictionary<string, object> kafkaDoc = LoadYaml(ProductionKafkaPath);

        // All configs must have subscriptionScopes metadata
        _ = GetComponentMetadataValue(localDoc, "subscriptionScopes").ShouldNotBeNull(
            "Local pub/sub must have subscriptionScopes");
        _ = GetComponentMetadataValue(rabbitDoc, "subscriptionScopes").ShouldNotBeNull(
            "Production RabbitMQ pub/sub must have subscriptionScopes");
        _ = GetComponentMetadataValue(kafkaDoc, "subscriptionScopes").ShouldNotBeNull(
            "Production Kafka pub/sub must have subscriptionScopes");

        // All configs must have publishingScopes metadata
        _ = GetComponentMetadataValue(localDoc, "publishingScopes").ShouldNotBeNull(
            "Local pub/sub must have publishingScopes");
        _ = GetComponentMetadataValue(rabbitDoc, "publishingScopes").ShouldNotBeNull(
            "Production RabbitMQ pub/sub must have publishingScopes");
        _ = GetComponentMetadataValue(kafkaDoc, "publishingScopes").ShouldNotBeNull(
            "Production Kafka pub/sub must have publishingScopes");

        // All configs must have component-level scopes restricting to eventstore
        VerifyScopesContainEventStore(localDoc, "local pub/sub");
        VerifyScopesContainEventStore(rabbitDoc, "production RabbitMQ pub/sub");
        VerifyScopesContainEventStore(kafkaDoc, "production Kafka pub/sub");

        // eventstore must NOT be listed in any scoping metadata (unrestricted access)
        foreach ((Dictionary<string, object> doc, string name) in new[]
        {
            (localDoc, "local"),
            (rabbitDoc, "production RabbitMQ"),
            (kafkaDoc, "production Kafka"),
        }) {
            string pubScopes = GetComponentMetadataValue(doc, "publishingScopes") ?? string.Empty;
            string subScopes = GetComponentMetadataValue(doc, "subscriptionScopes") ?? string.Empty;

            ShouldNotContainAppId(pubScopes, "eventstore",
                $"{name}: eventstore must NOT be listed in publishingScopes");
            ShouldNotContainAppId(subScopes, "eventstore",
                $"{name}: eventstore must NOT be listed in subscriptionScopes");
        }

        // All configs must have dead-letter enabled with consistent configuration
        GetComponentMetadataValue(localDoc, "enableDeadLetter").ShouldBe("true",
            "Local pub/sub must have dead-letter enabled");
        GetComponentMetadataValue(rabbitDoc, "enableDeadLetter").ShouldBe("true",
            "Production RabbitMQ pub/sub must have dead-letter enabled");
        GetComponentMetadataValue(kafkaDoc, "enableDeadLetter").ShouldBe("true",
            "Production Kafka pub/sub must have dead-letter enabled");
    }

    // --- Task 3.8: AC #10 (NFR20) ---

    [Fact]
    public void AllPubSubConfigs_EventStorePublishScope_NotRestricted() {
        // NFR20: eventstore must be able to publish to any topic (dynamic tenant provisioning).
        // This is achieved by NOT listing eventstore in publishingScopes (unlisted = unrestricted).
        // DAPR does NOT support wildcards -- "eventstore=*" would restrict to literal topic "*".
        foreach ((string path, string name) in new[]
        {
            (LocalPubSubPath, "local pub/sub"),
            (ProductionRabbitMqPath, "production RabbitMQ pub/sub"),
            (ProductionKafkaPath, "production Kafka pub/sub"),
        }) {
            Dictionary<string, object> doc = LoadYaml(path);

            string? publishingScopes = GetComponentMetadataValue(doc, "publishingScopes");
            _ = publishingScopes.ShouldNotBeNull(
                $"{name} must have publishingScopes metadata entry");

            // eventstore must NOT appear in publishingScopes
            ShouldNotContainAppId(publishingScopes!, "eventstore",
                $"{name}: eventstore must NOT be listed in publishingScopes -- " +
                "unlisted means unrestricted publish access to all topics including " +
                "dynamic tenant topics (NFR20). DAPR does not support wildcards.");

            // Verify no wildcard entries exist (common misconfiguration)
            publishingScopes!.Contains("=*").ShouldBeFalse(
                $"{name}: publishingScopes must not contain '=*' -- " +
                "DAPR treats * as a literal topic name, not a wildcard");
        }
    }

    // --- Task 3.9: AC #5 (regression) ---

    [Fact]
    public void AllPubSubConfigs_DomainServices_NoPubSubAccess() {
        // Regression test: domain services (sample) must have zero pub/sub access (Story 5.1).
        // Local config: sample denied via component scopes exclusion + explicit deny in scoping.
        // Production configs: sample not in component scopes = zero access.

        // Local: sample must be excluded from component scopes
        Dictionary<string, object> localDoc = LoadYaml(LocalPubSubPath);
        VerifySampleExcludedFromScopes(localDoc, "local pub/sub");

        // Local: sample must be explicitly denied in publishingScopes and subscriptionScopes
        string? localPubScopes = GetComponentMetadataValue(localDoc, "publishingScopes");
        _ = localPubScopes.ShouldNotBeNull("Local pub/sub must have publishingScopes");
        localPubScopes!.Contains("sample=").ShouldBeTrue(
            "Local pub/sub publishingScopes must deny sample publishing");

        string? localSubScopes = GetComponentMetadataValue(localDoc, "subscriptionScopes");
        _ = localSubScopes.ShouldNotBeNull("Local pub/sub must have subscriptionScopes");
        localSubScopes!.Contains("sample=").ShouldBeTrue(
            "Local pub/sub subscriptionScopes must deny sample subscription");

        // Production: sample must not be in component scopes (zero component access)
        Dictionary<string, object> rabbitDoc = LoadYaml(ProductionRabbitMqPath);
        VerifySampleExcludedFromScopes(rabbitDoc, "production RabbitMQ pub/sub");

        Dictionary<string, object> kafkaDoc = LoadYaml(ProductionKafkaPath);
        VerifySampleExcludedFromScopes(kafkaDoc, "production Kafka pub/sub");
    }

    [Fact]
    public void AllPubSubConfigs_DocumentUnauthorizedSubscriptionObservability() {
        foreach ((string path, string name) in new[]
        {
            (LocalPubSubPath, "local pub/sub"),
            (ProductionRabbitMqPath, "production RabbitMQ pub/sub"),
            (ProductionKafkaPath, "production Kafka pub/sub"),
        }) {
            string content = File.ReadAllText(path);

            content.Contains("unauthorized", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"{name} must document unauthorized subscription handling");
            content.Contains("sidecar", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"{name} must document that enforcement occurs in the DAPR sidecar");
            content.Contains("log", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"{name} must document sidecar log observability for unauthorized subscriptions");
        }
    }

    // --- YAML navigation helpers (matches AccessControlPolicyTests pattern) ---

    private static Dictionary<string, object> LoadYaml(string path) {
        string content = File.ReadAllText(path);
        return YamlParser.Deserialize<Dictionary<string, object>>(content);
    }

    private static object? Nav(object root, params string[] path) {
        object? current = root;
        foreach (string key in path) {
            current = current switch {
                Dictionary<string, object> stringDict when stringDict.TryGetValue(key, out object? val) => val,
                Dictionary<object, object> objDict when objDict.TryGetValue(key, out object? val) => val,
                _ => null,
            };
            if (current is null) {
                return null;
            }
        }

        return current;
    }

    private static List<object>? NavList(object root, params string[] path)
        => Nav(root, path) as List<object>;

    private static string GetString(Dictionary<object, object> map, string key)
        => map.TryGetValue(key, out object? val) ? val?.ToString() ?? string.Empty : string.Empty;

    private static string? GetComponentMetadataValue(Dictionary<string, object> doc, string metadataName) {
        List<object>? metadataList = NavList(doc, "spec", "metadata");
        if (metadataList is null) {
            return null;
        }

        Dictionary<object, object>? entry = metadataList
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(m => GetString(m, "name") == metadataName);
        return entry is not null ? GetString(entry, "value") : null;
    }

    private static void VerifyScopesContainEventStore(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have component-level scopes");
        scopes.Select(s => s?.ToString()).ShouldContain("eventstore",
            $"{componentName} scopes must include eventstore");
    }

    private static void VerifySampleExcludedFromScopes(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");
        scopes.Select(s => s?.ToString()).ShouldNotContain("sample",
            $"{componentName} scopes must NOT include sample -- zero-trust posture (D4)");
    }

    /// <summary>
    /// Verifies that an app-id does NOT appear in a scoping metadata value.
    /// Scoping format: "app1=topic1,topic2;app2=topic3" -- checks for "appId=" pattern.
    /// An app NOT listed in scoping metadata has UNRESTRICTED access (DAPR default-open behavior).
    /// </summary>
    private static void ShouldNotContainAppId(string scopingValue, string appId, string message) => scopingValue.Contains($"{appId}=").ShouldBeFalse(message);
}
