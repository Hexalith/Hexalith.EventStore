
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Story 5.3: Subscription scoping documentation tests (AC #7, #8, #10, #11).
/// Validates that all pub/sub YAML configurations contain required documentation
/// for subscriber onboarding, tenant scoping, deployment guidance, and dynamic
/// tenant provisioning strategy (NFR20).
/// Uses string-based content validation for documentation comments.
/// </summary>
public class SubscriptionScopingDocumentationTests {
    private static readonly string LocalPubSubPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "pubsub.yaml"));

    private static readonly string ProductionRabbitMqPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "pubsub-rabbitmq.yaml"));

    private static readonly string ProductionKafkaPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "pubsub-kafka.yaml"));

    // --- Task 4.2: AC #7 ---

    [Fact]
    public void LocalPubSubYaml_ContainsSubscriberOnboardingDocumentation() {
        string content = File.ReadAllText(LocalPubSubPath);

        // Must document how to add a new subscriber app-id
        content.Contains("ADDING A NEW SUBSCRIBER", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Local pub/sub YAML must document the subscriber onboarding procedure (AC #7)");

        // Must document the step to add subscriber to scopes
        content.Contains("scopes").ShouldBeTrue(
            "Local pub/sub YAML must mention adding subscriber to component scopes");

        // Must document the step to add subscriber to subscriptionScopes
        content.Contains("subscriptionScopes").ShouldBeTrue(
            "Local pub/sub YAML must mention configuring subscriptionScopes for the subscriber");

        // Must document that external subscribers should not publish
        content.Contains("publishingScopes").ShouldBeTrue(
            "Local pub/sub YAML must mention publishingScopes for subscriber restriction");
    }

    // --- Task 4.3: AC #7 ---

    [Fact]
    public void LocalPubSubYaml_ContainsTenantScopingDocumentation() {
        string content = File.ReadAllText(LocalPubSubPath);

        // Must document the three-layer scoping architecture
        content.Contains("Layer 1", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Local pub/sub YAML must document Layer 1 (component scoping)");
        content.Contains("Layer 2", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Local pub/sub YAML must document Layer 2 (publishing scoping)");
        content.Contains("Layer 3", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Local pub/sub YAML must document Layer 3 (subscription scoping)");

        // Must document dead-letter topic separation
        content.Contains("dead-letter", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Local pub/sub YAML must document dead-letter topic scoping separation (AC #6)");

        // Must document example subscriber scoping with tenant topics
        content.Contains("example-subscriber", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Local pub/sub YAML must include example subscriber scoping pattern");
    }

    // --- Task 4.4: AC #8 ---

    [Fact]
    public void ProductionPubSubYamls_ContainDeploymentSubstitutionGuidance() {
        string rabbitContent = File.ReadAllText(ProductionRabbitMqPath);
        string kafkaContent = File.ReadAllText(ProductionKafkaPath);

        // Production configs must document subscriber onboarding procedure
        rabbitContent.Contains("ADDING A NEW SUBSCRIBER", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Production RabbitMQ YAML must document subscriber onboarding (AC #8)");
        kafkaContent.Contains("ADDING A NEW SUBSCRIBER", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Production Kafka YAML must document subscriber onboarding (AC #8)");

        // Production configs must document placeholder app-id patterns
        rabbitContent.Contains("{subscriber-app-id}").ShouldBeTrue(
            "Production RabbitMQ YAML must use placeholder app-ids for deployment substitution");
        kafkaContent.Contains("{subscriber-app-id}").ShouldBeTrue(
            "Production Kafka YAML must use placeholder app-ids for deployment substitution");

        // Production configs must document production vs local differences
        rabbitContent.Contains("production", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Production RabbitMQ YAML must reference production context");
        kafkaContent.Contains("production", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
            "Production Kafka YAML must reference production context");
    }

    // --- Task 4.5: AC #10, #11 ---

    [Fact]
    public void AllPubSubConfigs_DocumentDynamicTenantStrategy() {
        string localContent = File.ReadAllText(LocalPubSubPath);
        string rabbitContent = File.ReadAllText(ProductionRabbitMqPath);
        string kafkaContent = File.ReadAllText(ProductionKafkaPath);

        // All configs must document the NFR20 dynamic tenant provisioning strategy
        foreach ((string content, string name) in new[]
        {
            (localContent, "Local pub/sub"),
            (rabbitContent, "Production RabbitMQ pub/sub"),
            (kafkaContent, "Production Kafka pub/sub"),
        }) {
            content.Contains("NFR20").ShouldBeTrue(
                $"{name} YAML must reference NFR20 (dynamic tenant provisioning)");

            content.Contains("DYNAMIC TENANT", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"{name} YAML must document the dynamic tenant provisioning strategy (AC #10, #11)");

            // Must document that publisher is unrestricted (no YAML changes needed)
            content.Contains("unrestricted", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"{name} YAML must document that publisher has unrestricted access");

            // Must document that wildcards are not supported
            content.Contains("wildcard", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
                $"{name} YAML must document that DAPR does not support wildcards in scoping");
        }
    }
}
