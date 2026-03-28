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

    private static readonly string LocalEventStoreAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "accesscontrol.yaml"));

    private static readonly string LocalAdminServerAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "accesscontrol.eventstore-admin.yaml"));

    private static readonly string LocalSampleAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "accesscontrol.sample.yaml"));

    private static readonly string ProductionEventStoreAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "accesscontrol.yaml"));

    private static readonly string ProductionAdminServerAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "accesscontrol.eventstore-admin.yaml"));

    private static readonly string ProductionSampleAccessControlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "accesscontrol.sample.yaml"));

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
    public void LocalEventStoreAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(LocalEventStoreAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Local EventStore access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Local EventStore access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Local EventStore access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Local EventStore access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Local EventStore access control must contain a policies section");
    }

    [Fact]
    public void LocalAdminServerAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(LocalAdminServerAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Local Admin.Server access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Local Admin.Server access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Local Admin.Server access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Local Admin.Server access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Local Admin.Server access control must contain a policies section");
    }

    [Fact]
    public void LocalSampleAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(LocalSampleAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Local sample access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Local sample access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Local sample access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Local sample access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Local sample access control must contain a policies section");
    }

    // --- Task 5.3: Local deny-by-default ---

    [Fact]
    public void LocalEventStoreAccessControlYaml_HasAllowDefault_SecureByDefault() {
        Dictionary<string, object> doc = LoadYaml(LocalEventStoreAccessControlPath);

        // Local self-hosted profile uses allow-by-default because mTLS identity is unavailable.
        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("allow",
            "EventStore global-level defaultAction must be allow in local self-hosted profile (no mTLS caller identity)");
    }

    // --- Task 5.4: EventStore policy completeness ---

    [Fact]
    public void LocalEventStoreAccessControlYaml_AdminServerPolicy_AllowsRequiredOperations() {
        Dictionary<string, object> doc = LoadYaml(LocalEventStoreAccessControlPath);

        Dictionary<object, object>? adminServerPolicy = FindPolicy(doc, "eventstore-admin");
        _ = adminServerPolicy.ShouldNotBeNull("Local EventStore access control must contain a policy for eventstore-admin");

        List<Dictionary<object, object>> operations = GetPolicyOperations(adminServerPolicy);
        operations.ShouldNotBeEmpty("eventstore-admin policy must have at least one operation");

        // Verify eventstore-admin can invoke EventStore via wildcard path with GET/POST/PUT.
        Dictionary<object, object>? wildcardOp = operations.FirstOrDefault(op =>
            GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull(
            "eventstore-admin policy must allow wildcard path (/**) for EventStore delegation (ADR-P4)");

        List<object>? httpVerbs = GetList(wildcardOp, "httpVerb");
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs.Select(v => v?.ToString()).ShouldContain("GET",
            "eventstore-admin policy must allow GET for EventStore read delegation");
        httpVerbs.Select(v => v?.ToString()).ShouldContain("POST",
            "eventstore-admin policy must allow POST for EventStore write delegation");
        httpVerbs.Select(v => v?.ToString()).ShouldContain("PUT",
            "eventstore-admin policy must allow PUT for EventStore update delegation");

        GetString(wildcardOp, "action").ShouldBe("allow",
            "eventstore-admin wildcard operation must have action: allow");
    }

    // --- Task 5.5: Sample domain service denied ---

    [Fact]
    public void LocalAdminServerAccessControlYaml_HasNoInboundPolicies() {
        Dictionary<string, object> doc = LoadYaml(LocalAdminServerAccessControlPath);

        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("allow",
            "Admin.Server global-level defaultAction must be allow in local self-hosted profile (no mTLS caller identity)");

        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull("Admin.Server access control must contain a policies list");
        policies.ShouldBeEmpty("Admin.Server should not allow any peer Dapr caller policies in the local topology");
    }

    [Fact]
    public void LocalSampleAccessControlYaml_EventStorePolicy_AllowsRequiredOperations() {
        Dictionary<string, object> doc = LoadYaml(LocalSampleAccessControlPath);

        Dictionary<object, object>? eventStorePolicy = FindPolicy(doc, "eventstore");
        _ = eventStorePolicy.ShouldNotBeNull("Local sample access control must contain a policy for eventstore");

        List<Dictionary<object, object>> operations = GetPolicyOperations(eventStorePolicy);
        operations.ShouldNotBeEmpty("eventstore policy must have at least one operation");

        Dictionary<object, object>? wildcardOp = operations.FirstOrDefault(op =>
            GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull(
            "eventstore policy must allow wildcard path (/**) for domain service invocation (D7)");

        List<object>? httpVerbs = GetList(wildcardOp, "httpVerb");
        _ = httpVerbs.ShouldNotBeNull("Wildcard operation must specify httpVerb");
        httpVerbs.Select(v => v?.ToString()).ShouldContain("POST",
            "eventstore policy must allow POST for service invocation");

        GetString(wildcardOp, "action").ShouldBe("allow",
            "eventstore wildcard operation must have action: allow");
    }

    // --- Task 5.6: Production access control YAML validation ---

    [Fact]
    public void ProductionEventStoreAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(ProductionEventStoreAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Production EventStore access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Production EventStore access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Production EventStore access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Production EventStore access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Production EventStore access control must contain a policies section");
    }

    [Fact]
    public void ProductionAdminServerAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(ProductionAdminServerAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Production Admin.Server access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Production Admin.Server access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Production Admin.Server access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Production Admin.Server access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Production Admin.Server access control must contain a policies section");
    }

    [Fact]
    public void ProductionSampleAccessControlYaml_IsValidYaml_ParsesCorrectly() {
        Dictionary<string, object> doc = LoadYaml(ProductionSampleAccessControlPath);

        Nav(doc, "apiVersion")?.ToString().ShouldBe("dapr.io/v1alpha1",
            "Production sample access control must use DAPR v1alpha1 API version");
        Nav(doc, "kind")?.ToString().ShouldBe("Configuration",
            "Production sample access control must be a Configuration CRD");
        _ = Nav(doc, "spec").ShouldNotBeNull(
            "Production sample access control must contain a spec section");
        _ = Nav(doc, "spec", "accessControl").ShouldNotBeNull(
            "Production sample access control must contain an accessControl section");
        _ = NavList(doc, "spec", "accessControl", "policies").ShouldNotBeNull(
            "Production sample access control must contain a policies section");
    }

    // --- Task 5.7: Production deny-by-default ---

    [Fact]
    public void ProductionEventStoreAccessControlYaml_HasDenyDefault_SecureByDefault() {
        Dictionary<string, object> doc = LoadYaml(ProductionEventStoreAccessControlPath);

        Nav(doc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("deny",
            "Production global-level defaultAction must be deny for secure-by-default posture (D4)");
    }

    // --- Task 5.8: Pub/sub scoping validation ---

    [Fact]
    public void PubSubYaml_HasScopes_RestrictsAppAccess() {
        // Verify local pub/sub has component-level scopes
        Dictionary<string, object> localDoc = LoadYaml(LocalPubSubPath);
        VerifyComponentScopedToEventStore(localDoc, "local pub/sub");

        // Verify production pub/sub configs have scopes
        Dictionary<string, object> rabbitDoc = LoadYaml(ProductionPubSubRabbitMqPath);
        VerifyComponentScopedToEventStore(rabbitDoc, "production RabbitMQ pub/sub");

        Dictionary<string, object> kafkaDoc = LoadYaml(ProductionPubSubKafkaPath);
        VerifyComponentScopedToEventStore(kafkaDoc, "production Kafka pub/sub");
    }

    // --- Task 5.9: State store scoping validation ---

    [Fact]
    public void StateStoreYaml_HasScopes_RestrictsAppAccess() {
        // Verify local state store has scopes
        Dictionary<string, object> localDoc = LoadYaml(LocalStateStorePath);
        VerifyComponentScopedToEventStore(localDoc, "local state store");

        // Verify production state store configs have scopes
        Dictionary<string, object> postgresDoc = LoadYaml(ProductionStateStorePostgresPath);
        VerifyComponentScopedToEventStore(postgresDoc, "production PostgreSQL state store");

        Dictionary<string, object> cosmosDoc = LoadYaml(ProductionStateStoreCosmosPath);
        VerifyComponentScopedToEventStore(cosmosDoc, "production Cosmos DB state store");
    }

    // --- Task 5.10: Local-production topology consistency ---

    [Fact]
    public void AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent() {
        Dictionary<string, object> localAc = LoadYaml(LocalSampleAccessControlPath);
        Dictionary<string, object> prodAc = LoadYaml(ProductionSampleAccessControlPath);

        // Local self-hosted profile is allow-by-default; production remains deny-by-default.
        Nav(localAc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("allow");
        Nav(prodAc, "spec", "accessControl", "defaultAction")?.ToString().ShouldBe("deny");

        // Both must have eventstore policy on the domain-service receiving sidecar config.
        _ = FindPolicy(localAc, "eventstore").ShouldNotBeNull("Local must have eventstore policy");
        _ = FindPolicy(prodAc, "eventstore").ShouldNotBeNull("Production must have eventstore policy");

        // Both must allow POST invocations with wildcard path
        List<Dictionary<object, object>> localOps = GetPolicyOperations(FindPolicy(localAc, "eventstore")!);
        List<Dictionary<object, object>> prodOps = GetPolicyOperations(FindPolicy(prodAc, "eventstore")!);
        localOps.Any(op => GetString(op, "name") == "/**").ShouldBeTrue("Local eventstore must have wildcard path");
        prodOps.Any(op => GetString(op, "name") == "/**").ShouldBeTrue("Production eventstore must have wildcard path");

        // Local self-hosted profile trust domain is public; production uses SPIFFE trust domain.
        Nav(localAc, "spec", "accessControl", "trustDomain")?.ToString().ShouldBe("public");
        string? prodTrustDomain = Nav(prodAc, "spec", "accessControl", "trustDomain")?.ToString();
        (prodTrustDomain is "hexalith.io" or "{env:DAPR_TRUST_DOMAIN|hexalith.io}").ShouldBeTrue(
            $"Production trust domain must be hexalith.io or env-parameterized with hexalith.io default, found '{prodTrustDomain}'");

        // Local pub/sub allows eventstore + explicitly authorized subscribers.
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(LocalPubSubPath),
            "local pub/sub");

        // Production pub/sub allows eventstore + explicitly authorized subscribers.
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubRabbitMqPath),
            "production RabbitMQ pub/sub");
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubKafkaPath),
            "production Kafka pub/sub");

        // All state store configs must restrict scopes to eventstore and eventstore-admin.
        VerifyStateStoreScopes(LoadYaml(LocalStateStorePath), "local state store");
        VerifyStateStoreScopes(LoadYaml(ProductionStateStorePostgresPath), "production PostgreSQL state store");
        VerifyStateStoreScopes(LoadYaml(ProductionStateStoreCosmosPath), "production Cosmos DB state store");
    }

    // --- Task 5.11: mTLS trust domain configuration ---

    [Fact]
    public void LocalAccessControlYaml_HasTrustDomain_MtlsConfigured() {
        Dictionary<string, object> doc = LoadYaml(LocalSampleAccessControlPath);

        Nav(doc, "spec", "accessControl", "trustDomain")?.ToString()
            .ShouldBe("public",
                "Local access control trust domain must be public in self-hosted mode without mTLS SPIFFE identity");
    }

    // --- Task 5.12: Dead-letter topic scoping ---

    [Fact]
    public void PubSubYaml_DeadLetterTopics_ScopedToEventStoreOnly() {
        Dictionary<string, object> localDoc = LoadYaml(LocalPubSubPath);

        // Verify dead-letter is enabled
        GetComponentMetadataValue(localDoc, "enableDeadLetter").ShouldBe("true",
            "Local pub/sub must have dead-letter enabled");
        _ = GetComponentMetadataValue(localDoc, "deadLetterTopic").ShouldNotBeNull(
            "Local pub/sub must configure a dead-letter topic");

        // Verify component scopes restrict to eventstore only
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
    public void ProductionPubSubYaml_DeadLetterTopics_ScopedToEventStoreOnly() {
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
        Dictionary<string, object> localEventStoreDoc = LoadYaml(LocalEventStoreAccessControlPath);
        Dictionary<object, object>? localAdminServer = FindPolicy(localEventStoreDoc, "eventstore-admin");
        _ = localAdminServer.ShouldNotBeNull();
        GetString(localAdminServer, "namespace").ShouldBe("default",
            "Local EventStore access control namespace must be 'default' for local development");

        Dictionary<string, object> localSampleDoc = LoadYaml(LocalSampleAccessControlPath);
        Dictionary<object, object>? localEventStore = FindPolicy(localSampleDoc, "eventstore");
        _ = localEventStore.ShouldNotBeNull();
        GetString(localEventStore, "namespace").ShouldBe("default",
            "Local sample access control namespace must be 'default' for local development");

        Dictionary<string, object> prodDoc = LoadYaml(ProductionSampleAccessControlPath);
        Dictionary<object, object>? prodEventStore = FindPolicy(prodDoc, "eventstore");
        _ = prodEventStore.ShouldNotBeNull();
        string prodNamespace = GetString(prodEventStore, "namespace");
        (prodNamespace is "hexalith" or "{env:DAPR_NAMESPACE|hexalith}").ShouldBeTrue(
            $"Production access control namespace must be 'hexalith' or env-parameterized with hexalith default, found '{prodNamespace}'");
    }

    // --- Task 5.13: Domain service zero-infrastructure-access ---

    [Fact]
    public void DomainServicePolicy_ZeroInfrastructureAccess_AllDenied() {
        // 1. Access control: sample sidecar only trusts eventstore and only for POST invocations.
        Dictionary<string, object> acDoc = LoadYaml(LocalSampleAccessControlPath);
        Dictionary<object, object>? eventStorePolicy = FindPolicy(acDoc, "eventstore");
        _ = eventStorePolicy.ShouldNotBeNull("Sample sidecar access control must contain eventstore policy");
        GetString(eventStorePolicy, "defaultAction").ShouldBe("deny",
            "eventstore caller policy must have defaultAction: deny in sample access control");

        List<Dictionary<object, object>> operations = GetPolicyOperations(eventStorePolicy);
        operations.Count.ShouldBe(1,
            "Sample sidecar should expose exactly one allowed wildcard operation to eventstore");

        Dictionary<object, object> wildcardOp = operations.Single();
        GetString(wildcardOp, "name").ShouldBe("/**",
            "Sample sidecar should only authorize the wildcard invocation path");
        List<object> verbs = GetList(wildcardOp, "httpVerb")!;
        verbs.Count.ShouldBe(1,
            "Sample sidecar should only authorize one HTTP verb for eventstore invocations");
        verbs[0]?.ToString().ShouldBe("POST",
            "Sample sidecar should only authorize POST invocations from eventstore");

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

        // 5. Production state-store configs: scopes must contain only eventstore and eventstore-admin
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubRabbitMqPath),
            "production RabbitMQ pub/sub");
        VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
            LoadYaml(ProductionPubSubKafkaPath),
            "production Kafka pub/sub");
        VerifyStateStoreScopes(LoadYaml(ProductionStateStorePostgresPath), "production PostgreSQL state store");
        VerifyStateStoreScopes(LoadYaml(ProductionStateStoreCosmosPath), "production Cosmos DB state store");

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
    public void EventStorePolicy_OnlyAllowsPOST_OtherVerbsBlocked() {
        // Defense-in-depth: verify the eventstore wildcard operation lists ONLY POST.
        // DaprClient.InvokeMethodAsync uses POST by default (D7). GET/PUT/DELETE must NOT
        // appear in the policy -- their presence would widen the attack surface.
        Dictionary<string, object> localDoc = LoadYaml(LocalSampleAccessControlPath);
        Dictionary<object, object> eventStorePolicy = FindPolicy(localDoc, "eventstore")!;
        Dictionary<object, object>? wildcardOp = GetPolicyOperations(eventStorePolicy)
            .FirstOrDefault(op => GetString(op, "name") == "/**");
        _ = wildcardOp.ShouldNotBeNull(
            "Local eventstore policy must contain wildcard operation (/**) for domain service invocation (D7)");

        List<object> httpVerbs = GetList(wildcardOp, "httpVerb")!;
        httpVerbs.Count.ShouldBe(1,
            "eventstore wildcard operation must list exactly one HTTP verb (POST only)");
        httpVerbs[0]?.ToString().ShouldBe("POST",
            "eventstore wildcard operation must allow POST only -- GET/PUT/DELETE are not permitted (D7)");

        // Same check for production
        Dictionary<string, object> prodDoc = LoadYaml(ProductionSampleAccessControlPath);
        Dictionary<object, object> prodPolicy = FindPolicy(prodDoc, "eventstore")!;
        Dictionary<object, object>? prodWildcardOp = GetPolicyOperations(prodPolicy)
            .FirstOrDefault(op => GetString(op, "name") == "/**");
        _ = prodWildcardOp.ShouldNotBeNull(
            "Production eventstore policy must contain wildcard operation (/**) for domain service invocation (D7)");

        List<object> prodHttpVerbs = GetList(prodWildcardOp, "httpVerb")!;
        prodHttpVerbs.Count.ShouldBe(1,
            "Production eventstore wildcard operation must list exactly one HTTP verb (POST only)");
        prodHttpVerbs[0]?.ToString().ShouldBe("POST",
            "Production eventstore wildcard operation must allow POST only (D7)");
    }

    // --- Story 5.4 Task 1.5.3: Local allow vs production deny intentional divergence guard (RT-2) ---

    [Fact]
    public void AccessControlYaml_LocalAllowAndProductionDeny_IntentionalDivergence() {
        // Single test asserting BOTH: local is allow AND production is deny.
        // Stronger than separate tests (#2, #6) -- catches accidental synchronization
        // if someone copies one config over the other (RT-2 finding).
        Dictionary<string, object> localDoc = LoadYaml(LocalSampleAccessControlPath);
        Dictionary<string, object> prodDoc = LoadYaml(ProductionSampleAccessControlPath);

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
    public void ProductionAccessControlYaml_OnlyEventStoreHasAllowedOperations() {
        // Forward-looking guard: in production accesscontrol.yaml, ONLY eventstore may have
        // allowed operations. If a future developer adds a domain service policy with operations,
        // this test catches the regression.
        Dictionary<string, object> doc = LoadYaml(ProductionSampleAccessControlPath);
        List<object>? policies = NavList(doc, "spec", "accessControl", "policies");
        _ = policies.ShouldNotBeNull("Production access control must have policies");

        foreach (Dictionary<object, object> policy in policies.Cast<Dictionary<object, object>>()) {
            string appId = GetString(policy, "appId");
            bool hasOperations = policy.ContainsKey("operations");

            if (appId != "eventstore") {
                hasOperations.ShouldBeFalse(
                    $"Production policy for '{appId}' must NOT have allowed operations. "
                    + "Only eventstore may have operations defined (D4 zero-trust posture).");
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
    /// Verifies that a DAPR component's scopes list contains eventstore.
    /// </summary>
    private static void VerifyComponentScopedToEventStore(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have component-level scopes");
        scopes.Select(s => s?.ToString()).ShouldContain("eventstore",
            $"{componentName} scopes must include eventstore");
    }

    /// <summary>
    /// Verifies that a state-store component's scopes list contains ONLY eventstore and eventstore-admin.
    /// </summary>
    private static void VerifyStateStoreScopes(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");
        scopes.ShouldNotBeEmpty($"{componentName} scopes section must have at least one entry");

        scopes.Select(scope => scope?.ToString()).ShouldBe(["eventstore", "eventstore-admin"], ignoreOrder: true,
            customMessage: $"{componentName} scopes must contain ONLY eventstore and eventstore-admin");
    }

    /// <summary>
    /// Verifies that sample is NOT in a DAPR component's scopes list.
    /// </summary>
    private static void VerifySampleExcludedFromScopes(Dictionary<string, object> doc, string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");
        scopes.Select(s => s?.ToString()).ShouldContain("eventstore",
            $"{componentName} scopes must include eventstore");
        scopes.Select(s => s?.ToString()).ShouldNotContain("sample",
            $"{componentName} scopes must NOT include sample -- zero-trust posture (AC #13)");
    }

    /// <summary>
    /// Verifies that production pub/sub scopes include eventstore plus explicit subscriber placeholders,
    /// and never include sample.
    /// </summary>
    private static void VerifyPubSubScopesAllowAuthorizedSubscribersOnly(
        Dictionary<string, object> doc,
        string componentName) {
        List<object>? scopes = doc.TryGetValue("scopes", out object? scopesObj) ? scopesObj as List<object> : null;
        _ = scopes.ShouldNotBeNull($"{componentName} must have a scopes section");

        string[] scopeValues = scopes.Select(s => s?.ToString() ?? string.Empty).ToArray();
        scopeValues.ShouldContain("eventstore",
            $"{componentName} scopes must include eventstore");
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
