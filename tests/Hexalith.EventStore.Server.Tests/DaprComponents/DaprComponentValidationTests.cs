
using Shouldly;

using YamlDotNet.Serialization;

namespace Hexalith.EventStore.Server.Tests.DaprComponents;
/// <summary>
/// Story 7.2: DAPR component validation tests.
/// Validates YAML structure and configuration correctness for all local DAPR component files.
/// Uses YamlDotNet for robust YAML parsing (following AccessControlPolicyTests pattern from Story 5.1).
/// </summary>
public class DaprComponentValidationTests {
    private static readonly IDeserializer YamlParser = new DeserializerBuilder().Build();

    private static readonly string DaprComponentsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents"));

    private static readonly string StateStorePath = Path.Combine(DaprComponentsDir, "statestore.yaml");
    private static readonly string PubSubPath = Path.Combine(DaprComponentsDir, "pubsub.yaml");
    private static readonly string ResiliencyPath = Path.Combine(DaprComponentsDir, "resiliency.yaml");
    private static readonly string AccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.yaml");
    private static readonly string SubscriptionPath = Path.Combine(DaprComponentsDir, "subscription-sample-counter.yaml");

    // --- Task 5.2: StateStoreComponent_HasActorStateStoreEnabled ---

    [Fact]
    public void StateStoreComponent_HasActorStateStoreEnabled() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        GetComponentMetadataValue(doc, "actorStateStore")
            .ShouldBe("true", "State store must have actorStateStore enabled for DAPR actor state management");
    }

    // --- Task 5.3: StateStoreComponent_ScopedToCommandApiOnly ---

    [Fact]
    public void StateStoreComponent_ScopedToCommandApiOnly() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        List<object>? scopes = GetScopes(doc);
        _ = scopes.ShouldNotBeNull("State store must have scopes defined");
        scopes.Count.ShouldBe(1, "State store scopes must contain exactly one entry (commandapi only)");
        scopes[0]?.ToString().ShouldBe("commandapi", "State store must be scoped to commandapi only (D4)");
    }

    // --- Task 5.4: PubSubComponent_HasDeadLetterEnabled ---

    [Fact]
    public void PubSubComponent_HasDeadLetterEnabled() {
        Dictionary<string, object> doc = LoadYaml(PubSubPath);
        GetComponentMetadataValue(doc, "enableDeadLetter")
            .ShouldBe("true", "Pub/sub must have dead-letter enabled for undeliverable message routing");
    }

    // --- Task 5.5: PubSubComponent_DenySamplePublishing ---

    [Fact]
    public void PubSubComponent_DenySamplePublishing() {
        Dictionary<string, object> doc = LoadYaml(PubSubPath);
        string? publishingScopes = GetComponentMetadataValue(doc, "publishingScopes");
        _ = publishingScopes.ShouldNotBeNull("Pub/sub must have publishingScopes defined");
        publishingScopes!.Contains("sample=").ShouldBeTrue(
            "Pub/sub publishingScopes must deny sample from publishing (empty value = deny all)");
    }

    // --- Task 5.6: PubSubComponent_DenySampleSubscription ---

    [Fact]
    public void PubSubComponent_DenySampleSubscription() {
        Dictionary<string, object> doc = LoadYaml(PubSubPath);
        string? subscriptionScopes = GetComponentMetadataValue(doc, "subscriptionScopes");
        _ = subscriptionScopes.ShouldNotBeNull("Pub/sub must have subscriptionScopes defined");
        subscriptionScopes!.Contains("sample=").ShouldBeTrue(
            "Pub/sub subscriptionScopes must deny sample from subscribing (empty value = deny all)");
    }

    // --- Task 5.7: AccessControl_DefaultActionIsDeny ---

    [Fact]
    public void AccessControl_DefaultActionIsDeny() {
        Dictionary<string, object> doc = LoadYaml(AccessControlPath);
        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString()
            .ShouldBe("deny", "Access control must have defaultAction: deny for secure-by-default posture (D4)");
    }

    // --- Task 5.8: AccessControl_CommandApiCanInvokePostOnly ---

    [Fact]
    public void AccessControl_CommandApiCanInvokePostOnly() {
        Dictionary<string, object> doc = LoadYaml(AccessControlPath);
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull();

        Dictionary<object, object>? commandApiPolicy = policies
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == "commandapi");
        _ = commandApiPolicy.ShouldNotBeNull("Access control must have a commandapi policy");

        List<object>? operations = commandApiPolicy.TryGetValue("operations", out object? ops)
            ? ops as List<object> : null;
        _ = operations.ShouldNotBeNull("commandapi policy must have operations");

        Dictionary<object, object>? wildcardOp = operations!
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(op => GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull("commandapi must allow wildcard path /** for domain service invocation (D7)");

        List<object>? httpVerbs = wildcardOp!.TryGetValue("httpVerb", out object? verbs) ? verbs as List<object> : null;
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs!.Select(v => v?.ToString()).ShouldContain("POST",
            "commandapi wildcard must allow POST");
        GetString(wildcardOp, "action").ShouldBe("allow");
    }

    // --- Task 5.9: AccessControl_SampleHasZeroAllowedOperations ---

    [Fact]
    public void AccessControl_SampleHasZeroAllowedOperations() {
        Dictionary<string, object> doc = LoadYaml(AccessControlPath);
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull();

        Dictionary<object, object>? samplePolicy = policies
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == "sample");
        _ = samplePolicy.ShouldNotBeNull("Access control must have a sample policy");

        GetString(samplePolicy, "defaultAction").ShouldBe("deny",
            "sample must have defaultAction: deny (zero-trust, D4)");
        samplePolicy.ContainsKey("operations").ShouldBeFalse(
            "sample must not have any allowed operations (zero infrastructure access)");
    }

    // --- Task 5.10: Resiliency_SidecarTimeoutIsFiveSeconds ---

    [Fact]
    public void Resiliency_SidecarTimeoutIsFiveSeconds() {
        Dictionary<string, object> doc = LoadYaml(ResiliencyPath);
        Nav(doc, "spec", "policies", "timeouts", "daprSidecar", "general")?.ToString()
            .ShouldBe("5s", "DAPR sidecar general timeout must be 5 seconds (Rule #14)");
    }

    // --- Task 5.11: Resiliency_PubSubHasCircuitBreaker ---

    [Fact]
    public void Resiliency_PubSubHasCircuitBreaker() {
        Dictionary<string, object> doc = LoadYaml(ResiliencyPath);

        // Verify pubsubBreaker policy exists
        _ = Nav(doc, "spec", "policies", "circuitBreakers", "pubsubBreaker").ShouldNotBeNull(
            "Resiliency must define a pubsubBreaker circuit breaker policy");

        // Verify it's wired to the pubsub component target
        Nav(doc, "spec", "targets", "components", "pubsub", "outbound", "circuitBreaker")?.ToString()
            .ShouldBe("pubsubBreaker", "Pub/sub outbound target must use pubsubBreaker circuit breaker");
    }

    // --- Task 5.12: AllComponentFiles_ExistInDaprComponentsDirectory ---

    [Fact]
    public void AllComponentFiles_ExistInDaprComponentsDirectory() {
        File.Exists(StateStorePath).ShouldBeTrue($"statestore.yaml must exist at {StateStorePath}");
        File.Exists(PubSubPath).ShouldBeTrue($"pubsub.yaml must exist at {PubSubPath}");
        File.Exists(ResiliencyPath).ShouldBeTrue($"resiliency.yaml must exist at {ResiliencyPath}");
        File.Exists(AccessControlPath).ShouldBeTrue($"accesscontrol.yaml must exist at {AccessControlPath}");
        File.Exists(SubscriptionPath).ShouldBeTrue($"subscription-sample-counter.yaml must exist at {SubscriptionPath}");
    }

    // --- Additional: Resiliency has statestore component target (Task 2.1 validation) ---

    [Fact]
    public void Resiliency_HasStateStoreComponentTarget() {
        Dictionary<string, object> doc = LoadYaml(ResiliencyPath);
        _ = Nav(doc, "spec", "targets", "components", "statestore").ShouldNotBeNull(
            "Resiliency must have a statestore component target for event persistence retry/circuit breaker (D1, D2)");
        Nav(doc, "spec", "targets", "components", "statestore", "retry")?.ToString()
            .ShouldNotBeNullOrEmpty("Statestore target must have a retry policy");
        Nav(doc, "spec", "targets", "components", "statestore", "circuitBreaker")?.ToString()
            .ShouldNotBeNullOrEmpty("Statestore target must have a circuit breaker policy");
    }

    // --- YAML navigation helpers (same pattern as AccessControlPolicyTests) ---

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
