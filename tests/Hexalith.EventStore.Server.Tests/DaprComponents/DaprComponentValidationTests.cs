
using Shouldly;

using static Hexalith.EventStore.Server.Tests.DaprComponents.DaprYamlTestHelper;

namespace Hexalith.EventStore.Server.Tests.DaprComponents;
/// <summary>
/// Story 7.2: DAPR component validation tests.
/// Validates YAML structure and configuration correctness for all local DAPR component files.
/// Uses YamlDotNet for robust YAML parsing (following AccessControlPolicyTests pattern from Story 5.1).
/// </summary>
public class DaprComponentValidationTests {
    private static readonly string DaprComponentsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents"));

    private static readonly string StateStorePath = Path.Combine(DaprComponentsDir, "statestore.yaml");
    private static readonly string PubSubPath = Path.Combine(DaprComponentsDir, "pubsub.yaml");
    private static readonly string ResiliencyPath = Path.Combine(DaprComponentsDir, "resiliency.yaml");
    private static readonly string EventStoreAccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.yaml");
    private static readonly string AdminServerAccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.eventstore-admin.yaml");
    private static readonly string SampleAccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.sample.yaml");
    private static readonly string SubscriptionPath = Path.Combine(DaprComponentsDir, "subscription-sample-counter.yaml");

    // --- Task 5.2: StateStoreComponent_HasActorStateStoreEnabled ---

    [Fact]
    public void StateStoreComponent_HasActorStateStoreEnabled() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        GetComponentMetadataValue(doc, "actorStateStore")
            .ShouldBe("true", "State store must have actorStateStore enabled for DAPR actor state management");
    }

    // --- Task 5.3: StateStoreComponent_ScopedToEventStoreAndAdminServer ---

    [Fact]
    public void StateStoreComponent_ScopedToEventStoreAndAdminServer() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        List<object>? scopes = GetScopes(doc);
        _ = scopes.ShouldNotBeNull("State store must have scopes defined");
        scopes.Count.ShouldBe(2, "State store scopes must contain exactly two entries (eventstore and eventstore-admin)");
        scopes.Select(s => s?.ToString()).ShouldBe(["eventstore", "eventstore-admin"], ignoreOrder: true);
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
    public void EventStoreAccessControl_DefaultActionIsAllowInLocalProfile() {
        Dictionary<string, object> doc = LoadYaml(EventStoreAccessControlPath);
        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString()
            .ShouldBe("allow", "Local EventStore access control uses defaultAction: allow in self-hosted profile (no mTLS caller identity)");
    }

    // --- Task 5.8: AccessControl_EventStoreCanInvokePostOnly ---

    [Fact]
    public void EventStoreAccessControl_AdminServerCanInvokeGetPostPut() {
        Dictionary<string, object> doc = LoadYaml(EventStoreAccessControlPath);
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull();

        Dictionary<object, object>? adminServerPolicy = policies
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == "eventstore-admin");
        _ = adminServerPolicy.ShouldNotBeNull("EventStore access control must have an eventstore-admin policy");

        List<object>? operations = adminServerPolicy.TryGetValue("operations", out object? ops)
            ? ops as List<object> : null;
        _ = operations.ShouldNotBeNull("eventstore-admin policy must have operations");

        Dictionary<object, object>? wildcardOp = operations!
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(op => GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull("eventstore-admin must allow wildcard path /** for EventStore delegation");

        List<object>? httpVerbs = wildcardOp!.TryGetValue("httpVerb", out object? verbs) ? verbs as List<object> : null;
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs!.Select(v => v?.ToString()).ShouldContain("GET",
            "eventstore-admin wildcard must allow GET");
        httpVerbs!.Select(v => v?.ToString()).ShouldContain("POST",
            "eventstore-admin wildcard must allow POST");
        httpVerbs!.Select(v => v?.ToString()).ShouldContain("PUT",
            "eventstore-admin wildcard must allow PUT");
        GetString(wildcardOp, "action").ShouldBe("allow");
    }

    // --- Task 5.9: AccessControl_SampleHasZeroAllowedOperations ---

    [Fact]
    public void AdminServerAccessControl_HasNoInboundPolicies() {
        Dictionary<string, object> doc = LoadYaml(AdminServerAccessControlPath);

        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString()
            .ShouldBe("allow", "Local Admin.Server access control uses defaultAction: allow in self-hosted profile (no mTLS caller identity)");

        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull("Admin.Server access control must contain a policies list");
        policies.ShouldBeEmpty("Admin.Server should not expose inbound Dapr caller policies in the local topology");
    }

    [Fact]
    public void SampleAccessControl_EventStoreCanInvokePostOnly() {
        Dictionary<string, object> doc = LoadYaml(SampleAccessControlPath);
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull();

        Dictionary<object, object>? eventStorePolicy = policies
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == "eventstore");
        _ = eventStorePolicy.ShouldNotBeNull("Sample access control must have a eventstore policy");

        GetString(eventStorePolicy, "defaultAction").ShouldBe("deny",
            "eventstore caller policy must have defaultAction: deny (zero-trust, D4)");

        List<object>? operations = eventStorePolicy.TryGetValue("operations", out object? ops)
            ? ops as List<object> : null;
        _ = operations.ShouldNotBeNull("eventstore policy must have operations");
        operations.Count.ShouldBe(1, "Sample access control should allow exactly one wildcard POST operation for eventstore");

        Dictionary<object, object>? wildcardOp = operations!
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(op => GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull("eventstore must allow wildcard path /** for domain service invocation (D7)");

        List<object>? httpVerbs = wildcardOp!.TryGetValue("httpVerb", out object? verbs) ? verbs as List<object> : null;
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs.Count.ShouldBe(1, "Sample wildcard operation must allow exactly one verb");
        httpVerbs[0]?.ToString().ShouldBe("POST", "eventstore wildcard must allow POST only");
        GetString(wildcardOp, "action").ShouldBe("allow");
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
        File.Exists(EventStoreAccessControlPath).ShouldBeTrue($"accesscontrol.yaml must exist at {EventStoreAccessControlPath}");
        File.Exists(AdminServerAccessControlPath).ShouldBeTrue($"accesscontrol.eventstore-admin.yaml must exist at {AdminServerAccessControlPath}");
        File.Exists(SampleAccessControlPath).ShouldBeTrue($"accesscontrol.sample.yaml must exist at {SampleAccessControlPath}");
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

}
