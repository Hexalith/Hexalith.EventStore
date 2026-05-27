
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

    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string StateStorePath = Path.Combine(DaprComponentsDir, "statestore.yaml");
    private static readonly string PubSubPath = Path.Combine(DaprComponentsDir, "pubsub.yaml");
    private static readonly string ResiliencyPath = Path.Combine(DaprComponentsDir, "resiliency.yaml");
    private static readonly string EventStoreAccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.yaml");
    private static readonly string AdminServerAccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.eventstore-admin.yaml");
    private static readonly string SampleAccessControlPath = Path.Combine(DaprComponentsDir, "accesscontrol.sample.yaml");
    private static readonly string SubscriptionPath = Path.Combine(DaprComponentsDir, "subscription-sample-counter.yaml");
    private static readonly string ConfigurationReferencePath = Path.Combine(ProjectRoot, "docs", "guides", "configuration-reference.md");
    private static readonly string DaprComponentReferencePath = Path.Combine(ProjectRoot, "docs", "guides", "dapr-component-reference.md");
    private static readonly string EventVersioningPath = Path.Combine(ProjectRoot, "docs", "concepts", "event-versioning.md");
    private static readonly string DomainServiceOptionsApiReferencePath = Path.Combine(ProjectRoot, "docs", "reference", "api", "Hexalith.EventStore.Server", "Hexalith.EventStore.Server.DomainServices.DomainServiceOptions.md");

    // --- Task 5.2: StateStoreComponent_HasActorStateStoreEnabled ---

    [Fact]
    public void StateStoreComponent_HasActorStateStoreEnabled() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        GetComponentMetadataValue(doc, "actorStateStore")
            .ShouldBe("true", "State store must have actorStateStore enabled for DAPR actor state management");
    }

    [Fact]
    public void StateStoreComponent_HasStableNameRedisTypeAndLocalMetadata() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);

        Nav(doc, "metadata", "name")?.ToString().ShouldBe("statestore");
        Nav(doc, "spec", "type")?.ToString().ShouldBe("state.redis");
        GetComponentMetadataValue(doc, "redisHost")
            .ShouldBe("{env:REDIS_HOST|127.0.0.1:6379}", "Local statestore must keep the YAML-owned Redis host fallback");
        GetComponentMetadataValue(doc, "keyPrefix")
            .ShouldBe("none", "Admin.Server reads EventStore-owned keys through a separate DAPR app-id");
    }

    [Fact]
    public void StateStoreComponent_IsLoadedFromIsolatedYamlByAppHost() {
        string appHost = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Hexalith.EventStore.AppHost", "Program.cs"));
        string extension = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Hexalith.EventStore.Aspire", "HexalithEventStoreExtensions.cs"));

        appHost.ShouldContain("stateStoreComponentPath = ResolveDaprConfigPath(\"statestore.yaml\")");
        appHost.ShouldContain("isolatedStateStoreComponentPath = ResolveIsolatedDaprComponentPath(stateStoreComponentPath)");
        appHost.ShouldContain("stateStoreComponentPath: isolatedStateStoreComponentPath");
        extension.ShouldContain("File.Exists(resolvedStateStoreComponentPath)");
        extension.ShouldContain("new DaprComponentOptions { LocalPath = resolvedStateStoreComponentPath }");
    }

    [Fact]
    public void StateStoreFallbackMetadata_MatchesAuthoritativeYaml() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        string extension = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Hexalith.EventStore.Aspire", "HexalithEventStoreExtensions.cs"));
        string? redisHostFallback = GetComponentMetadataValue(doc, "redisHost")
            ?.Replace("{env:REDIS_HOST|", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal);

        extension.ShouldContain($"const string LocalDaprRedisHost = \"{redisHostFallback}\";");
        extension.ShouldContain($".WithMetadata(\"actorStateStore\", \"{GetComponentMetadataValue(doc, "actorStateStore")}\")");
        extension.ShouldContain(".WithMetadata(\"redisHost\", LocalDaprRedisHost)");
        extension.ShouldContain($".WithMetadata(\"keyPrefix\", \"{GetComponentMetadataValue(doc, "keyPrefix")}\")");
    }

    // --- Task 5.3: StateStoreComponent_ScopedToEventStoreAdminServerAndTenants ---

    [Fact]
    public void StateStoreComponent_ScopedToEventStoreAdminServerAndTenants() {
        Dictionary<string, object> doc = LoadYaml(StateStorePath);
        List<object>? scopes = GetScopes(doc);
        _ = scopes.ShouldNotBeNull("State store must have scopes defined");
        scopes.Count.ShouldBe(3, "State store scopes must contain exactly three entries (eventstore, eventstore-admin, and tenants)");
        scopes.Select(s => s?.ToString()).ShouldBe(["eventstore", "eventstore-admin", "tenants"], ignoreOrder: true);
        scopes.Select(s => s?.ToString()).ShouldNotContain("sample");
        scopes.Select(s => s?.ToString()).ShouldNotContain("sample-blazor-ui");
        scopes.Select(s => s?.ToString()).ShouldNotContain("eventstore-admin-ui");
    }

    [Fact]
    public void DomainServiceSidecars_DoNotReferenceStateStoreOrPubSubComponents() {
        string appHost = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Hexalith.EventStore.AppHost", "Program.cs"));

        string sampleBlock = GetBlock(appHost, "IResourceBuilder<ProjectResource> sample =", "IResourceBuilder<ProjectResource> blazorUi =");
        sampleBlock.ShouldNotContain("eventStoreResources.StateStore");
        sampleBlock.ShouldNotContain("eventStoreResources.PubSub");
        sampleBlock.ShouldContain("ResourcesPaths = ImmutableHashSet.Create(emptyDaprResourcesPath)");

        string tenantsBlock = GetBlock(appHost, "IResourceBuilder<ProjectResource> tenants =", "IResourceBuilder<ProjectResource> sample =");
        tenantsBlock.ShouldContain("eventStoreResources.StateStore");
        tenantsBlock.ShouldContain("eventStoreResources.PubSub");

        string sampleBlazorBlock = GetBlock(appHost, "IResourceBuilder<ProjectResource> blazorUi =", "if (keycloak is not null");
        sampleBlazorBlock.ShouldNotContain("eventStoreResources.StateStore");
        sampleBlazorBlock.ShouldNotContain("eventStoreResources.PubSub");

        string adminUiBlock = GetBlock(
            File.ReadAllText(Path.Combine(ProjectRoot, "src", "Hexalith.EventStore.Aspire", "HexalithEventStoreExtensions.cs")),
            "if (adminUI is not null)",
            "return new HexalithEventStoreResources");
        adminUiBlock.ShouldNotContain("WithReference(stateStore)");
        adminUiBlock.ShouldNotContain("WithReference(pubSub)");
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
    public void AdminServerAccessControl_AllowsAdminUiOnly() {
        Dictionary<string, object> doc = LoadYaml(AdminServerAccessControlPath);

        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString()
            .ShouldBe("allow", "Local Admin.Server access control uses defaultAction: allow in self-hosted profile (no mTLS caller identity)");

        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull("Admin.Server access control must contain a policies list");
        policies.Count.ShouldBe(1, "Admin.Server should only allow the Admin.UI caller in the local topology");

        Dictionary<object, object> adminUiPolicy = policies.Cast<Dictionary<object, object>>().Single();
        GetString(adminUiPolicy, "appId").ShouldBe("eventstore-admin-ui");
        GetString(adminUiPolicy, "defaultAction").ShouldBe("deny");

        List<object>? operations = adminUiPolicy.TryGetValue("operations", out object? ops)
            ? ops as List<object> : null;
        _ = operations.ShouldNotBeNull("eventstore-admin-ui policy must have operations");
        operations.Count.ShouldBe(1, "Admin.UI should have one wildcard Admin.Server operation grant");

        Dictionary<object, object> wildcardOp = operations.Cast<Dictionary<object, object>>().Single();
        GetString(wildcardOp, "name").ShouldBe("/**");
        GetString(wildcardOp, "action").ShouldBe("allow");
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

    [Fact]
    public void StateStoreDocumentation_DoesNotContainStaleLocalSourceOfTruthMetadata() {
        string[] paths =
        [
            Path.Combine(ProjectRoot, "docs", "guides", "dapr-component-reference.md"),
            Path.Combine(ProjectRoot, "docs", "guides", "configuration-reference.md"),
            Path.Combine(ProjectRoot, "docs", "guides", "deployment-docker-compose.md"),
            Path.Combine(ProjectRoot, "docs", "getting-started", "first-domain-service.md"),
            Path.Combine(ProjectRoot, "docs", "guides", "security-model.md"),
        ];

        string joined = string.Join(Environment.NewLine, paths.Select(File.ReadAllText));

        joined.ShouldNotContain("{env:REDIS_HOST|localhost:6379}");
        joined.ShouldNotContain("| `EventStore:CommandStatus:StateStoreName` | string | `\"eventstore\"` |");
        joined.ShouldNotContain("policies: []");
        joined.ShouldContain("Authoritative local source: `src/Hexalith.EventStore.AppHost/DaprComponents/statestore.yaml`");
        joined.ShouldContain("`DaprComponentOptions.LocalPath`");
        joined.ShouldContain("`keyPrefix: \"none\"`");
        joined.ShouldContain("`eventstore` / `eventstore-admin` / `tenants` state-store scopes");
    }

    [Fact]
    public void DomainServiceDocumentation_ConfigStoreNameIsOptInAndNotDefaultConfigstore() {
        string configurationReference = File.ReadAllText(ConfigurationReferencePath);
        string daprReference = File.ReadAllText(DaprComponentReferencePath);
        string eventVersioning = File.ReadAllText(EventVersioningPath);
        string apiReference = File.ReadAllText(DomainServiceOptionsApiReferencePath);
        string joined = string.Join(Environment.NewLine, configurationReference, daprReference, eventVersioning, apiReference);

        configurationReference.ShouldContain("| `ConfigStoreName` | string? | `null` |");
        configurationReference.ShouldContain("| `EventStore:DomainServices:ConfigStoreName` | string? | `null` |");
        apiReference.ShouldContain("Default: null");
        apiReference.ShouldContain("public string? ConfigStoreName");
        joined.ShouldContain("config-store routing is opt-in");
        joined.ShouldContain("Optional domain service registration and dynamic config");
        joined.ShouldContain("Optional Configuration Store");
        joined.ShouldContain("No `configstore` component is registered out of the box");
        joined.ShouldNotContain("| `ConfigStoreName` | string | `\"configstore\"` |");
        joined.ShouldNotContain("| `EventStore:DomainServices:ConfigStoreName` | string | `\"configstore\"` |");
        joined.ShouldNotContain("Default: \"configstore\"");
    }

    [Fact]
    public void DomainServiceDocumentation_PublishesSupportedRegistrationKeyFormats() {
        string configurationReference = File.ReadAllText(ConfigurationReferencePath);
        string eventVersioning = File.ReadAllText(EventVersioningPath);
        string joined = string.Join(Environment.NewLine, configurationReference, eventVersioning);

        joined.ShouldContain("`tenant|domain|version`");
        joined.ShouldContain("`tenant:domain:version`");
        joined.ShouldContain("`*|domain|version`");
        joined.ShouldContain("`wildcard_{domain}_{version}`");
        joined.ShouldContain("pipe wildcard");
        joined.ShouldContain("sanitized wildcard");
        joined.ShouldContain("not portable environment-variable name characters");
        joined.ShouldContain("`*|party|v1`");
        joined.ShouldContain("`wildcard_party_v1`");
    }

    [Fact]
    public void DomainServiceDocumentation_PublishesMatchingResolverPrecedenceOrder() {
        string configurationReference = File.ReadAllText(ConfigurationReferencePath);
        string eventVersioning = File.ReadAllText(EventVersioningPath);

        AssertDomainServicePrecedenceOrder(configurationReference);
        AssertDomainServicePrecedenceOrder(eventVersioning);
    }

    private static string GetBlock(string content, string startMarker, string endMarker) {
        int start = content.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"Expected to find marker '{startMarker}'");

        int end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, $"Expected to find marker '{endMarker}' after '{startMarker}'");

        return content[start..end];
    }

    private static void AssertDomainServicePrecedenceOrder(string content) {
        string[] orderedMarkers =
        [
            "1. exact static registration keyed by `tenant:domain:version`",
            "2. exact static registration keyed by `tenant|domain|version`",
            "3. pipe wildcard static registration keyed by `*|domain|version`",
            "4. sanitized wildcard static registration keyed by `wildcard_{domain}_{version}`",
            "5. opt-in DAPR config-store lookup",
            "6. convention fallback: `AppId = domain`, `MethodName = \"process\"`",
        ];

        int previousIndex = -1;
        foreach (string marker in orderedMarkers) {
            int index = content.IndexOf(marker, StringComparison.Ordinal);
            index.ShouldBeGreaterThan(previousIndex, $"Expected domain-service precedence marker '{marker}' in order");
            previousIndex = index;
        }
    }

}
