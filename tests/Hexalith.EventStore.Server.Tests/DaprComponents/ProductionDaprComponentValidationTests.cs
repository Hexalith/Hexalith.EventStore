
using Shouldly;

using YamlDotNet.Serialization;

namespace Hexalith.EventStore.Server.Tests.DaprComponents;
/// <summary>
/// Story 7.3: Production DAPR component validation tests.
/// Validates structural correctness of production configs in deploy/dapr/ and
/// verifies parity with local configs (NFR29).
/// </summary>
public class ProductionDaprComponentValidationTests {
    private static readonly IDeserializer YamlParser = new DeserializerBuilder().Build();

    private static readonly string DeployDaprDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr"));

    private static readonly string LocalDaprDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents"));

    // --- Task 6.2: AllProductionComponentFiles_ExistInDeployDaprDirectory ---

    [Fact]
    public void AllProductionComponentFiles_ExistInDeployDaprDirectory() {
        string[] expectedFiles =
        [
            "statestore-postgresql.yaml",
            "statestore-cosmosdb.yaml",
            "pubsub-rabbitmq.yaml",
            "pubsub-kafka.yaml",
            "pubsub-servicebus.yaml",
            "resiliency.yaml",
            "accesscontrol.yaml",
            "subscription-sample-counter.yaml",
        ];

        foreach (string file in expectedFiles) {
            string path = Path.Combine(DeployDaprDir, file);
            File.Exists(path).ShouldBeTrue($"{file} must exist in deploy/dapr/ directory at {path}");
        }
    }

    // --- Task 6.3: ProductionStateStores_HaveActorStateStoreEnabled ---

    [Theory]
    [InlineData("statestore-postgresql.yaml")]
    [InlineData("statestore-cosmosdb.yaml")]
    public void ProductionStateStores_HaveActorStateStoreEnabled(string fileName) {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, fileName));
        GetComponentMetadataValue(doc, "actorStateStore")
            .ShouldBe("true", $"{fileName} must have actorStateStore enabled for DAPR actor state management");
    }

    // --- Task 6.4: ProductionStateStores_ScopedToCommandApiOnly ---

    [Theory]
    [InlineData("statestore-postgresql.yaml")]
    [InlineData("statestore-cosmosdb.yaml")]
    public void ProductionStateStores_ScopedToCommandApiOnly(string fileName) {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, fileName));
        List<object>? scopes = GetScopes(doc);
        _ = scopes.ShouldNotBeNull($"{fileName} must have scopes defined");
        scopes.Count.ShouldBe(1, $"{fileName} scopes must contain exactly one entry (commandapi only)");
        scopes[0]?.ToString().ShouldBe("commandapi", $"{fileName} must be scoped to commandapi only (D4)");
    }

    // --- Task 6.5: ProductionPubSubs_HaveDeadLetterEnabled ---

    [Theory]
    [InlineData("pubsub-rabbitmq.yaml")]
    [InlineData("pubsub-kafka.yaml")]
    [InlineData("pubsub-servicebus.yaml")]
    public void ProductionPubSubs_HaveDeadLetterEnabled(string fileName) {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, fileName));
        GetComponentMetadataValue(doc, "enableDeadLetter")
            .ShouldBe("true", $"{fileName} must have dead-letter enabled for undeliverable message routing");
    }

    [Theory]
    [InlineData("pubsub-rabbitmq.yaml")]
    [InlineData("pubsub-kafka.yaml")]
    [InlineData("pubsub-servicebus.yaml")]
    public void ProductionPubSubs_DeadLetterTopicIsConfigured(string fileName) {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, fileName));
        GetComponentMetadataValue(doc, "deadLetterTopic")
            .ShouldBe("deadletter", $"{fileName} must set deadLetterTopic to the shared dead-letter topic name");
    }

    // --- Task 6.6: ProductionAccessControl_DefaultActionIsDeny ---

    [Fact]
    public void ProductionAccessControl_DefaultActionIsDeny() {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, "accesscontrol.yaml"));
        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString()
            .ShouldBe("deny", "Production access control must have defaultAction: deny (D4)");
    }

    // --- Task 6.7: ProductionAccessControl_CommandApiCanPostOnly ---

    [Fact]
    public void ProductionAccessControl_CommandApiCanPostOnly() {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, "accesscontrol.yaml"));
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull();

        Dictionary<object, object>? commandApiPolicy = policies
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == "commandapi");
        _ = commandApiPolicy.ShouldNotBeNull("Production access control must have a commandapi policy");

        List<object>? operations = commandApiPolicy.TryGetValue("operations", out object? ops)
            ? ops as List<object> : null;
        _ = operations.ShouldNotBeNull("commandapi policy must have operations");

        Dictionary<object, object>? wildcardOp = operations!
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(op => GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull("commandapi must allow wildcard path /** for domain service invocation (D7)");

        List<object>? httpVerbs = wildcardOp!.TryGetValue("httpVerb", out object? verbs) ? verbs as List<object> : null;
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs!.Select(v => v?.ToString()).ShouldContain("POST", "commandapi wildcard must allow POST");
        GetString(wildcardOp, "action").ShouldBe("allow");
    }

    // --- Task 6.8: ProductionAccessControl_NoSampleDomainService ---

    [Fact]
    public void ProductionAccessControl_NoSampleDomainService() {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, "accesscontrol.yaml"));
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull();

        Dictionary<object, object>? samplePolicy = policies
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == "sample");
        samplePolicy.ShouldBeNull("Production access control must NOT include sample domain service (production-only config)");
    }

    // --- Task 6.9: ProductionResiliency_HasStatestoreTarget ---

    [Fact]
    public void ProductionResiliency_HasStatestoreTarget() {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, "resiliency.yaml"));
        _ = Nav(doc, "spec", "targets", "components", "statestore").ShouldNotBeNull(
            "Production resiliency must have a statestore component target");
        Nav(doc, "spec", "targets", "components", "statestore", "retry")?.ToString()
            .ShouldNotBeNullOrEmpty("Statestore target must have a retry policy");
        Nav(doc, "spec", "targets", "components", "statestore", "circuitBreaker")?.ToString()
            .ShouldNotBeNullOrEmpty("Statestore target must have a circuit breaker policy");
    }

    // --- Task 6.10: ProductionResiliency_SidecarTimeoutIsFiveSeconds ---

    [Fact]
    public void ProductionResiliency_SidecarTimeoutIsFiveSeconds() {
        Dictionary<string, object> doc = LoadYaml(Path.Combine(DeployDaprDir, "resiliency.yaml"));
        Nav(doc, "spec", "policies", "timeouts", "daprSidecar", "general")?.ToString()
            .ShouldBe("5s", "Production DAPR sidecar general timeout must be 5 seconds (Rule #14)");
    }

    // --- Task 6.11: LocalAndProduction_StateStoreComponentNames_Match ---

    [Fact]
    public void LocalAndProduction_StateStoreComponentNames_Match() {
        Dictionary<string, object> localDoc = LoadYaml(Path.Combine(LocalDaprDir, "statestore.yaml"));
        Dictionary<string, object> prodPgDoc = LoadYaml(Path.Combine(DeployDaprDir, "statestore-postgresql.yaml"));
        Dictionary<string, object> prodCosmosDoc = LoadYaml(Path.Combine(DeployDaprDir, "statestore-cosmosdb.yaml"));

        string? localName = Nav(localDoc, "metadata", "name")?.ToString();
        string? prodPgName = Nav(prodPgDoc, "metadata", "name")?.ToString();
        string? prodCosmosName = Nav(prodCosmosDoc, "metadata", "name")?.ToString();

        localName.ShouldBe("statestore");
        prodPgName.ShouldBe("statestore", "PostgreSQL state store component name must match local (NFR29)");
        prodCosmosName.ShouldBe("statestore", "Cosmos DB state store component name must match local (NFR29)");
    }

    // --- Task 6.12: LocalAndProduction_PubSubComponentNames_Match ---

    [Fact]
    public void LocalAndProduction_PubSubComponentNames_Match() {
        Dictionary<string, object> localDoc = LoadYaml(Path.Combine(LocalDaprDir, "pubsub.yaml"));
        Dictionary<string, object> prodRmqDoc = LoadYaml(Path.Combine(DeployDaprDir, "pubsub-rabbitmq.yaml"));
        Dictionary<string, object> prodKafkaDoc = LoadYaml(Path.Combine(DeployDaprDir, "pubsub-kafka.yaml"));
        Dictionary<string, object> prodSbDoc = LoadYaml(Path.Combine(DeployDaprDir, "pubsub-servicebus.yaml"));

        string? localName = Nav(localDoc, "metadata", "name")?.ToString();
        string? prodRmqName = Nav(prodRmqDoc, "metadata", "name")?.ToString();
        string? prodKafkaName = Nav(prodKafkaDoc, "metadata", "name")?.ToString();
        string? prodSbName = Nav(prodSbDoc, "metadata", "name")?.ToString();

        localName.ShouldBe("pubsub");
        prodRmqName.ShouldBe("pubsub", "RabbitMQ pub/sub component name must match local (NFR29)");
        prodKafkaName.ShouldBe("pubsub", "Kafka pub/sub component name must match local (NFR29)");
        prodSbName.ShouldBe("pubsub", "Service Bus pub/sub component name must match local (NFR29)");
    }

    // --- Task 6.13: DeployReadme_Exists ---

    [Fact]
    public void DeployReadme_Exists() {
        string readmePath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "deploy", "README.md"));
        File.Exists(readmePath).ShouldBeTrue($"deploy/README.md must exist at {readmePath}");
        string content = File.ReadAllText(readmePath);
        content.Length.ShouldBeGreaterThan(500, "deploy/README.md must be non-trivial (comprehensive deployment guide)");
    }

    // --- YAML navigation helpers (same pattern as DaprComponentValidationTests) ---

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

    private static List<object>? GetScopes(Dictionary<string, object> doc)
        => doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;

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
}
