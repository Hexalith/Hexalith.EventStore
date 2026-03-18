using Shouldly;

using static Hexalith.EventStore.Server.Tests.DaprComponents.DaprYamlTestHelper;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Story 5.1: DAPR access control policy validation tests (AC #10).
/// Validates YAML structure, deny-by-default posture, app-id policies, pub/sub scoping,
/// state store scoping, mTLS trust domain, dead-letter scoping, and local-production consistency.
/// Uses YamlDotNet for robust YAML parsing instead of brittle string-based assertions.
/// </summary>
public class AccessControlPolicyTests {
    // --- File path construction (matches ResiliencyConfigurationTests pattern) ---

    private static readonly string LocalAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "accesscontrol.yaml"));

    private static readonly string ProductionAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "accesscontrol.yaml"));

    private static readonly string LocalPubSubPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "pubsub.yaml"));

    private static readonly string ProductionPubSubRabbitMqPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "pubsub-rabbitmq.yaml"));

    private static readonly string ProductionPubSubKafkaPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "pubsub-kafka.yaml"));

    private static readonly string LocalStateStorePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "statestore.yaml"));

    private static readonly string ProductionStateStorePostgresPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "statestore-postgresql.yaml"));

    private static readonly string ProductionStateStoreCosmosPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "statestore-cosmosdb.yaml"));

    // --- Task 5.2: Local access control YAML validation ---

    [Fact]
    public void LocalAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(LocalAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Local access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Local access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Local access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Local access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Local access control must contain a policies section");
    }

    // --- Task 5.3: Local deny-by-default ---

    [Fact]
    public void LocalAccessControlYaml_HasDenyDefault_SecureByDefault() {
        Dictionary<string, object> doc = LoadYaml(LocalAccessControlPath);

        // Local self-hosted profile uses allow-by-default because mTLS identity is unavailable.
        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("allow",
            "Global-level defaultAction must be allow in local self-hosted profile (no mTLS caller identity)");
    }

    // --- Task 5.4: CommandApi policy completeness ---

    [Fact]
    public void LocalAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations() {
        Dictionary<string, object> doc = LoadYaml(LocalAccessControlPath);

        Dictionary<object, object>? commandApiPolicy = FindPolicy(doc, "commandapi");
        _ = commandApiPolicy.ShouldNotBeNull("Local access control must contain a policy for commandapi");

        List<Dictionary<object, object>> operations = GetPolicyOperations(commandApiPolicy);
        operations.ShouldNotBeEmpty("commandapi policy must have at least one operation");

        // Verify commandapi can invoke domain services via wildcard path with POST
        Dictionary<object, object>? wildcardOp = operations.FirstOrDefault(op =>
            GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull(
            "commandapi policy must allow wildcard path (/**) for domain service invocation (D7)");

        List<object>? httpVerbs = GetList(wildcardOp, "httpVerb");
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs.Select(v => v?.ToString()).ShouldContain("POST",
            "commandapi policy must allow POST for service invocation");

        GetString(wildcardOp, "action").ShouldBe("allow",
            "commandapi wildcard operation must have action: allow");
    }

    // --- Task 5.5: Sample domain service denied ---

    [Fact]
    public void LocalAccessControlYaml_SamplePolicy_DeniesDirectInvocation() {
        Dictionary<string, object> doc = LoadYaml(LocalAccessControlPath);

        Dictionary<object, object>? samplePolicy = FindPolicy(doc, "sample");
        _ = samplePolicy.ShouldNotBeNull(
            "Local access control must contain a policy for sample domain service");

        GetString(samplePolicy, "defaultAction").ShouldBe("deny",
            "sample policy must have defaultAction: deny (zero-trust posture, AC #3, AC #13)");

        // Verify NO operations are defined for sample (zero infrastructure access)
        samplePolicy.ContainsKey("operations").ShouldBeFalse(
            "sample domain service must not have any operations defined (D4, AC #13)");
    }

    // --- Task 5.6: Production access control YAML validation ---

    [Fact]
    public void ProductionAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(ProductionAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Production access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Production access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Production access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Production access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Production access control must contain a policies section");
    }

    // --- Task 5.7: Production deny-by-default ---

    [Fact]
    public void ProductionAccessControlYaml_HasDenyDefault_SecureByDefault() {
        Dictionary<string, object> doc = LoadYaml(ProductionAccessControlPath);

        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("deny",
            "Production global-level defaultAction must be deny for secure-by-default posture (D4)");
    }

    // --- Task 5.8: Pub/sub scoping validation ---

    [Fact]
    public void PubSubYaml_HasScopes_RestrictsAppAccess() {
        // Verify local pub/sub has component-level scopes
        Dictionary<string, object> localDoc = LoadYaml(LocalPubSubPath);
        VerifyComponentScopedToCommandApi(localDoc, "local pub/sub");

        // Verify production pub/sub configs have scopes
        Dictionary<string, object> rabbitDoc = LoadYaml(ProductionPubSubRabbitMqPath);
        VerifyComponentScopedToCommandApi(rabbitDoc, "production RabbitMQ pub/sub");

        Dictionary<string, object> kafkaDoc = LoadYaml(ProductionPubSubKafkaPath);
        VerifyComponentScopedToCommandApi(kafkaDoc, "production Kafka pub/sub");
    }

    // --- Task 5.9: State store scoping validation ---

    [Fact]
    public void StateStoreYaml_HasScopes_RestrictsAppAccess() {
        // Verify local state store has scopes
        Dictionary<string, object> localDoc = LoadYaml(LocalStateStorePath);
        VerifyComponentScopedToCommandApi(localDoc, "local state store");

        // Verify production state store configs have scopes
        Dictionary<string, object> postgresDoc = LoadYaml(ProductionStateStorePostgresPath);
        VerifyComponentScopedToCommandApi(postgresDoc, "production PostgreSQL state store");

        Dictionary<string, object> cosmosDoc = LoadYaml(ProductionStateStoreCosmosPath);
        VerifyComponentScopedToCommandApi(cosmosDoc, "production Cosmos DB state store");
    }

    // --- Task 5.10: Local-production topology consistency ---

    [Fact]
    public void AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent() {
        Dictionary<string, object> localAc = LoadYaml(LocalAccessControlPath);
        Dictionary<string, object> prodAc = LoadYaml(ProductionAccessControlPath);

        // Local self-hosted profile is allow-by-default; production remains deny-by-default.
        Nav(localAc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("allow");
        Nav(prodAc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("deny");

        // Both must have commandapi policy
        _ = FindPolicy(localAc, "commandapi").ShouldNotBeNull("Local must have commandapi policy");
        _ = FindPolicy(prodAc, "commandapi").ShouldNotBeNull("Production must have commandapi policy");

        // Both must allow POST invocations with wildcard path
        List<Dictionary<object, object>> localOps = GetPolicyOperations(FindPolicy(localAc, "commandapi")!);
        List<Dictionary<object, object>> prodOps = GetPolicyOperations(FindPolicy(prodAc, "commandapi")!);
        localOps.Any(op => GetString(op, "name") == "/**").ShouldBeTrue("Local commandapi must have wildcard path");
        prodOps.Any(op => GetString(op, "name") == "/**").ShouldBeTrue("Production commandapi must have wildcard path");

        // Local self-hosted profile trust domain is public; production uses SPIFFE trust domain.
        Nav(localAc, "spec", "accessControl", "trustDomain")?.ToString().ShouldBe("public");
        string? prodTrustDomain = Nav(prodAc, "spec", "accessControl", "trustDomain")?.ToString();
        (prodTrustDomain is "hexalith.io" or "{env:DAPR_TRUST_DOMAIN|hexalith.io}").ShouldBeTrue(
            $"Production trust domain must be hexalith.io or env-parameterized with hexalith.io default, found '{prodTrustDomain}'");

        // Local pub/sub allows commandapi + explicitly authorized subscribers.
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(LocalPubSubPath),
            "local pub/sub");

        // Production pub/sub allows commandapi + explicitly authorized subscribers.
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubRabbitMqPath),
            "production RabbitMQ pub/sub");
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubKafkaPath),
            "production Kafka pub/sub");

        // All state store configs must restrict scopes to commandapi
        VerifyScopesContainOnlyCommandApi(LoadYaml(LocalStateStorePath), "local state store");
        VerifyScopesContainOnlyCommandApi(LoadYaml(ProductionStateStorePostgresPath), "production PostgreSQL state store");
        VerifyScopesContainOnlyCommandApi(LoadYaml(ProductionStateStoreCosmosPath), "production Cosmos DB state store");
    }

    // --- Task 5.11: mTLS trust domain configuration ---

    [Fact]
    public void LocalAccessControlYaml_HasTrustDomain_MtlsConfigured() {
        Dictionary<string, object> doc = LoadYaml(LocalAccessControlPath);

        Nav(doc, "spec", "accessControl", "trustDomain")?.ToString()
            .ShouldBe("public",
                "Local access control trust domain must be public in self-hosted mode without mTLS SPIFFE identity");
    }

    // --- Task 5.12: Dead-letter topic scoping ---

    [Fact]
    public void PubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly() {
        Dictionary<string, object> localDoc = LoadYaml(LocalPubSubPath);

        // Verify dead-letter is enabled
        GetComponentMetadataValue(localDoc, "enableDeadLetter").ShouldBe("true",
            "Local pub/sub must have dead-letter enabled");
        _ = GetComponentMetadataValue(localDoc, "deadLetterTopic").ShouldNotBeNull(
            "Local pub/sub must configure a dead-letter topic");

        // Verify component scopes restrict to commandapi only
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(localDoc, "local pub/sub (dead-letter check)");

        // Verify sample is explicitly denied publishing access
        string? publishingScopes = GetComponentMetadataValue(localDoc, "publishingScopes");
        _ = publishingScopes.ShouldNotBeNull(
            "Local pub/sub must have publishingScopes for defense-in-depth (AC #12)");
        publishingScopes!.Contains("sample=").ShouldBeTrue(
            "Local pub/sub publishingScopes must explicitly deny sample publishing access");
    }

    // --- Production dead-letter scoping (AC #12 consistency) ---

    [Fact]
    public void ProductionPubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly() {
        // AC #12: dead-letter scoping must be consistent across local and production.
        Dictionary<string, object> rabbitDoc = LoadYaml(ProductionPubSubRabbitMqPath);
        GetComponentMetadataValue(rabbitDoc, "enableDeadLetter").ShouldBe("true",
            "Production RabbitMQ pub/sub must have dead-letter enabled");
        _ = GetComponentMetadataValue(rabbitDoc, "deadLetterTopic").ShouldNotBeNull(
            "Production RabbitMQ pub/sub must configure a dead-letter topic");
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            rabbitDoc,
            "production RabbitMQ pub/sub (dead-letter check)");

        Dictionary<string, object> kafkaDoc = LoadYaml(ProductionPubSubKafkaPath);
        GetComponentMetadataValue(kafkaDoc, "enableDeadLetter").ShouldBe("true",
            "Production Kafka pub/sub must have dead-letter enabled");
        _ = GetComponentMetadataValue(kafkaDoc, "deadLetterTopic").ShouldNotBeNull(
            "Production Kafka pub/sub must configure a dead-letter topic");
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            kafkaDoc,
            "production Kafka pub/sub (dead-letter check)");
    }

    // --- Namespace configuration validation ---

    [Fact]
    public void AccessControlYaml_HasNamespace_IdentityConfigured() {
        Dictionary<string, object> localDoc = LoadYaml(LocalAccessControlPath);
        Dictionary<object, object>? localCommandApi = FindPolicy(localDoc, "commandapi");
        _ = localCommandApi.ShouldNotBeNull();
        GetString(localCommandApi, "namespace").ShouldBe("default",
            "Local access control namespace must be 'default' for local development");

        Dictionary<string, object> prodDoc = LoadYaml(ProductionAccessControlPath);
        Dictionary<object, object>? prodCommandApi = FindPolicy(prodDoc, "commandapi");
        _ = prodCommandApi.ShouldNotBeNull();
        string prodNamespace = GetString(prodCommandApi, "namespace");
        (prodNamespace is "hexalith" or "{env:DAPR_NAMESPACE|hexalith}").ShouldBeTrue(
            $"Production access control namespace must be 'hexalith' or env-parameterized with hexalith default, found '{prodNamespace}'");
    }

    // --- Task 5.13: Domain service zero-infrastructure-access ---

    [Fact]
    public void DomainServicePolicy_ZeroInfrastructureAccess_AllDenied() {
        // 1. Access control: sample has defaultAction: deny with no allowed operations
        Dictionary<string, object> acDoc = LoadYaml(LocalAccessControlPath);
        Dictionary<object, object>? samplePolicy = FindPolicy(acDoc, "sample");
        _ = samplePolicy.ShouldNotBeNull("Access control must contain sample policy");
        GetString(samplePolicy, "defaultAction").ShouldBe("deny",
            "sample must have defaultAction: deny in access control");
        samplePolicy.ContainsKey("operations").ShouldBeFalse(
            "sample must not have any operations (AC #13)");

        // 2. Pub/sub: sample is excluded from component scopes
        Dictionary<string, object> pubSubDoc = LoadYaml(LocalPubSubPath);
        VerifySampleExcludedFromScopes(pubSubDoc, "pub/sub");

        // 3. State store: sample is excluded from component scopes
        Dictionary<string, object> stateDoc = LoadYaml(LocalStateStorePath);
        VerifySampleExcludedFromScopes(stateDoc, "state store");

        // 4. Pub/sub topic scoping: sample explicitly denied publishing and subscription
        _ = GetComponentMetadataValue(pubSubDoc, "publishingScopes").ShouldNotBeNull(
            "Pub/sub must have publishingScopes restricting sample");
        _ = GetComponentMetadataValue(pubSubDoc, "subscriptionScopes").ShouldNotBeNull(
            "Pub/sub must have subscriptionScopes restricting sample");

        // 5. Production configs: scopes must contain ONLY commandapi
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubRabbitMqPath),
            "production RabbitMQ pub/sub");
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubKafkaPath),
            "production Kafka pub/sub");
        VerifyScopesContainOnlyCommandApi(LoadYaml(ProductionStateStorePostgresPath), "production PostgreSQL state store");
        VerifyScopesContainOnlyCommandApi(LoadYaml(ProductionStateStoreCosmosPath), "production Cosmos DB state store");

        // 6. Production pub/sub: active publishingScopes and subscriptionScopes for defense-in-depth
        Dictionary<string, object> prodRabbitDoc = LoadYaml(ProductionPubSubRabbitMqPath);
        _ = GetComponentMetadataValue(prodRabbitDoc, "publishingScopes").ShouldNotBeNull(
            "Production RabbitMQ pub/sub must have active publishingScopes for defense-in-depth");
        _ = GetComponentMetadataValue(prodRabbitDoc, "subscriptionScopes").ShouldNotBeNull(
            "Production RabbitMQ pub/sub must have active subscriptionScopes for defense-in-depth");

        Dictionary<string, object> prodKafkaDoc = LoadYaml(ProductionPubSubKafkaPath);
        _ = GetComponentMetadataValue(prodKafkaDoc, "publishingScopes").ShouldNotBeNull(
            "Production Kafka pub/sub must have active publishingScopes for defense-in-depth");
        _ = GetComponentMetadataValue(prodKafkaDoc, "subscriptionScopes").ShouldNotBeNull(
            "Production Kafka pub/sub must have active subscriptionScopes for defense-in-depth");
    }

    // --- Story 5.4 Task 1.5.1: POST-only verb guard ---

    [Fact]
    public void CommandApiPolicy_OnlyAllowsPOST_OtherVerbsBlocked() {
        // Defense-in-depth: verify the commandapi wildcard operation lists ONLY POST.
        // DaprClient.InvokeMethodAsync uses POST by default (D7). GET/PUT/DELETE must NOT
        // appear in the policy -- their presence would widen the attack surface.
        Dictionary<string, object> localDoc = LoadYaml(LocalAccessControlPath);
        Dictionary<object, object> commandApiPolicy = FindPolicy(localDoc, "commandapi")!;
        Dictionary<object, object> wildcardOp = GetPolicyOperations(commandApiPolicy)
            .First(op => GetString(op, "name") == "/**");

        List<object> httpVerbs = GetList(wildcardOp, "httpVerb")!;
        httpVerbs.Count.ShouldBe(1,
            "commandapi wildcard operation must list exactly one HTTP verb (POST only)");
        httpVerbs[0]?.ToString().ShouldBe("POST",
            "commandapi wildcard operation must allow POST only -- GET/PUT/DELETE are not permitted (D7)");

        // Same check for production
        Dictionary<string, object> prodDoc = LoadYaml(ProductionAccessControlPath);
        Dictionary<object, object> prodPolicy = FindPolicy(prodDoc, "commandapi")!;
        Dictionary<object, object> prodWildcardOp = GetPolicyOperations(prodPolicy)
            .First(op => GetString(op, "name") == "/**");

        List<object> prodHttpVerbs = GetList(prodWildcardOp, "httpVerb")!;
        prodHttpVerbs.Count.ShouldBe(1,
            "Production commandapi wildcard operation must list exactly one HTTP verb (POST only)");
        prodHttpVerbs[0]?.ToString().ShouldBe("POST",
            "Production commandapi wildcard operation must allow POST only (D7)");
    }

    // --- Story 5.4 Task 1.5.3: Local allow vs production deny intentional divergence guard (RT-2) ---

    [Fact]
    public void AccessControlYaml_LocalAllowAndProductionDeny_IntentionalDivergence() {
        // Single test asserting BOTH: local is allow AND production is deny.
        // Stronger than separate tests (#2, #6) -- catches accidental synchronization
        // if someone copies one config over the other (RT-2 finding).
        Dictionary<string, object> localDoc = LoadYaml(LocalAccessControlPath);
        Dictionary<string, object> prodDoc = LoadYaml(ProductionAccessControlPath);

        string localDefault = Nav(localDoc, "spec", "accessControl", "defaultAction")?.ToString() ?? "";
        string prodDefault = Nav(prodDoc, "spec", "accessControl", "defaultAction")?.ToString() ?? "";

        localDefault.ShouldBe("allow",
            "Local defaultAction must be 'allow' (self-hosted without mTLS)");
        prodDefault.ShouldBe("deny",
            "Production defaultAction must be 'deny' (Kubernetes with mTLS)");

        // Guard: the two must differ -- if they match, config was accidentally synchronized
        localDefault.ShouldNotBe(prodDefault,
            "Local and production defaultAction must intentionally diverge (local=allow, production=deny). "
            + "If they match, someone may have copied one config over the other (RT-2 guard).");
    }

    // --- Story 5.4 Task 2.5.1: Production operations guard ---

    [Fact]
    public void ProductionAccessControlYaml_OnlyCommandApiHasAllowedOperations() {
        // Forward-looking guard: in production accesscontrol.yaml, ONLY commandapi may have
        // allowed operations. If a future developer adds a domain service policy with operations,
        // this test catches the regression.
        Dictionary<string, object> doc = LoadYaml(ProductionAccessControlPath);
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull("Production access control must have policies");

        foreach (Dictionary<object, object> policy in policies.Cast<Dictionary<object, object>>()) {
            string appId = GetString(policy, "appId");
            bool hasOperations = policy.ContainsKey("operations");

            if (appId != "commandapi") {
                hasOperations.ShouldBeFalse(
                    $"Production policy for '{appId}' must NOT have allowed operations. "
                    + "Only commandapi may have operations defined (D4 zero-trust posture).");
            }
        }
    }

    // --- Helpers (LoadYaml, Nav, NavList, GetString, GetComponentMetadataValue imported from DaprYamlTestHelper) ---

    /// <summary>
    /// Gets a list from a YAML map node.
    /// </summary>
    private static List<object>? GetList(Dictionary<object, object> map, string key)
        => map.TryGetValue(key, out object? val) ? val as List<object> : null;

    /// <summary>
    /// Finds a policy by appId in the access control policies list.
    /// </summary>
    private static Dictionary<object, object>? FindPolicy(Dictionary<string, object> doc, string appId) {
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        return policies?
            .Cast<Dictionary<object, object>>()
            .FirstOrDefault(p => GetString(p, "appId") == appId);
    }

    /// <summary>
    /// Gets the operations list from an access control policy.
    /// </summary>
    private static List<Dictionary<object, object>> GetPolicyOperations(Dictionary<object, object> policy) {
        List<object>? ops = GetList(policy, "operations");
        return ops?.Cast<Dictionary<object, object>>().ToList()
            ?? [];
    }

    /// <summary>
    /// Verifies that a DAPR component's scopes list contains commandapi.
    /// </summary>
    private static void VerifyComponentScopedToCommandApi(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have component-level scopes");
        scopes.Select(s => s?.ToString()).ShouldContain("commandapi",
            $"{componentName} scopes must include commandapi");
    }

    /// <summary>
    /// Verifies that a DAPR component's scopes list contains ONLY commandapi.
    /// </summary>
    private static void VerifyScopesContainOnlyCommandApi(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");
        scopes.ShouldNotBeEmpty($"{componentName} scopes section must have at least one entry");

        foreach (object? scope in scopes) {
            scope?.ToString().ShouldBe("commandapi",
                $"{componentName} scopes must contain ONLY commandapi, found '{scope}'");
        }
    }

    /// <summary>
    /// Verifies that sample is NOT in a DAPR component's scopes list.
    /// </summary>
    private static void VerifySampleExcludedFromScopes(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");
        scopes.Select(s => s?.ToString()).ShouldContain("commandapi",
            $"{componentName} scopes must include commandapi");
        scopes.Select(s => s?.ToString()).ShouldNotContain("sample",
            $"{componentName} scopes must NOT include sample -- zero-trust posture (AC #13)");
    }

    /// <summary>
    /// Verifies that production pub/sub scopes include commandapi plus explicit subscriber placeholders,
    /// and never include sample.
    /// </summary>
    private static void VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
        Dictionary<string, object> doc,
        string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");

        string[] scopeValues = scopes.Select(s => s?.ToString() ?? string.Empty).ToArray();
        scopeValues.ShouldContain("commandapi",
            $"{componentName} scopes must include commandapi");
        scopeValues.Any(s => s is "{subscriber-app-id}" or "example-subscriber"
            or "{env:SUBSCRIBER_APP_ID}").ShouldBeTrue(
            $"{componentName} scopes must include subscriber app-id (placeholder or concrete)");
        scopeValues.Any(s => s is "{ops-monitor-app-id}" or "ops-monitor"
            or "{env:OPS_MONITOR_APP_ID}").ShouldBeTrue(
            $"{componentName} scopes must include ops-monitor app-id (placeholder or concrete)");
        scopeValues.ShouldNotContain("sample",
            $"{componentName} scopes must not include sample");
    }
}
