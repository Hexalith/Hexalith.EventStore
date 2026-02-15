namespace Hexalith.EventStore.Server.Tests.Security;

using Shouldly;

/// <summary>
/// Story 5.1: DAPR access control policy validation tests (AC #10).
/// Validates YAML structure, deny-by-default posture, app-id policies, pub/sub scoping,
/// state store scoping, mTLS trust domain, dead-letter scoping, and local-production consistency.
/// Uses string-based validation matching the ResiliencyConfigurationTests pattern (no YamlDotNet).
/// </summary>
public class AccessControlPolicyTests
{
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
    public void LocalAccessControlYaml_IsValidYaml_ParsesCorrectly()
    {
        string content = File.ReadAllText(LocalAccessControlPath);

        content.ShouldContain("apiVersion: dapr.io/v1alpha1", customMessage:
            "Local access control must use DAPR v1alpha1 API version");
        content.ShouldContain("kind: Configuration", customMessage:
            "Local access control must be a Configuration CRD");
        content.ShouldContain("spec:", customMessage:
            "Local access control must contain a spec section");
        content.ShouldContain("accessControl:", customMessage:
            "Local access control must contain an accessControl section");
        content.ShouldContain("policies:", customMessage:
            "Local access control must contain a policies section");
    }

    // --- Task 5.3: Local deny-by-default ---

    [Fact]
    public void LocalAccessControlYaml_HasDenyDefault_SecureByDefault()
    {
        string content = File.ReadAllText(LocalAccessControlPath);

        content.ShouldContain("defaultAction: deny", customMessage:
            "Local access control must use defaultAction: deny for secure-by-default posture (D4)");

        // Verify the GLOBAL default is deny (must appear before the first policies: line)
        int policiesIndex = content.IndexOf("policies:", StringComparison.Ordinal);
        policiesIndex.ShouldBeGreaterThan(-1, "Access control must have a policies section");
        string globalSection = content[..policiesIndex];
        globalSection.ShouldContain("defaultAction: deny", customMessage:
            "Global-level defaultAction must be deny (not just per-policy defaults)");
    }

    // --- Task 5.4: CommandApi policy completeness ---

    [Fact]
    public void LocalAccessControlYaml_CommandApiPolicy_AllowsRequiredOperations()
    {
        string content = File.ReadAllText(LocalAccessControlPath);

        content.ShouldContain("appId: commandapi", customMessage:
            "Local access control must contain a policy for commandapi");
        content.ShouldContain("action: allow", customMessage:
            "commandapi policy must have at least one allowed operation");

        // Verify commandapi can invoke domain services via wildcard path
        content.ShouldContain("/**", customMessage:
            "commandapi policy must allow wildcard path for domain service invocation (D7)");
        content.ShouldContain("POST", customMessage:
            "commandapi policy must allow POST for service invocation");
    }

    // --- Task 5.5: Sample domain service denied ---

    [Fact]
    public void LocalAccessControlYaml_SamplePolicy_DeniesDirectInvocation()
    {
        string content = File.ReadAllText(LocalAccessControlPath);

        content.ShouldContain("appId: sample", customMessage:
            "Local access control must contain a policy for sample domain service");

        // Verify sample's policy section has defaultAction: deny and no allowed operations.
        // Extract the sample policy section (from "appId: sample" to end of file or next policy)
        int samplePolicyStart = content.IndexOf("appId: sample", StringComparison.Ordinal);
        samplePolicyStart.ShouldBeGreaterThan(-1);
        string sampleSection = content[samplePolicyStart..];

        sampleSection.ShouldContain("defaultAction: deny", customMessage:
            "sample policy must have defaultAction: deny (zero-trust posture, AC #3, AC #13)");

        // Verify NO "action: allow" appears in the sample section
        sampleSection.ShouldNotContain("action: allow", customMessage:
            "sample domain service must not have any allowed operations (D4, AC #13)");
    }

    // --- Task 5.6: Production access control YAML validation ---

    [Fact]
    public void ProductionAccessControlYaml_IsValidYaml_ParsesCorrectly()
    {
        string content = File.ReadAllText(ProductionAccessControlPath);

        content.ShouldContain("apiVersion: dapr.io/v1alpha1", customMessage:
            "Production access control must use DAPR v1alpha1 API version");
        content.ShouldContain("kind: Configuration", customMessage:
            "Production access control must be a Configuration CRD");
        content.ShouldContain("spec:", customMessage:
            "Production access control must contain a spec section");
        content.ShouldContain("accessControl:", customMessage:
            "Production access control must contain an accessControl section");
        content.ShouldContain("policies:", customMessage:
            "Production access control must contain a policies section");
    }

    // --- Task 5.7: Production deny-by-default ---

    [Fact]
    public void ProductionAccessControlYaml_HasDenyDefault_SecureByDefault()
    {
        string content = File.ReadAllText(ProductionAccessControlPath);

        content.ShouldContain("defaultAction: deny", customMessage:
            "Production access control must use defaultAction: deny for secure-by-default posture (D4)");

        // Verify the GLOBAL default is deny (must appear before the first policies: line)
        int policiesIndex = content.IndexOf("policies:", StringComparison.Ordinal);
        policiesIndex.ShouldBeGreaterThan(-1, "Production access control must have a policies section");
        string globalSection = content[..policiesIndex];
        globalSection.ShouldContain("defaultAction: deny", customMessage:
            "Production global-level defaultAction must be deny (not just per-policy defaults)");
    }

    // --- Task 5.8: Pub/sub scoping validation ---

    [Fact]
    public void PubSubYaml_HasScopes_RestrictsAppAccess()
    {
        // Verify local pub/sub has component-level scopes
        string localContent = File.ReadAllText(LocalPubSubPath);
        localContent.ShouldContain("scopes:", customMessage:
            "Local pub/sub must have component-level scopes (AC #7)");
        localContent.ShouldContain("commandapi", customMessage:
            "Local pub/sub scopes must include commandapi");

        // Verify production pub/sub configs have scopes
        string rabbitContent = File.ReadAllText(ProductionPubSubRabbitMqPath);
        rabbitContent.ShouldContain("scopes:", customMessage:
            "Production RabbitMQ pub/sub must have component-level scopes (AC #7)");
        rabbitContent.ShouldContain("commandapi", customMessage:
            "Production RabbitMQ pub/sub scopes must include commandapi");

        string kafkaContent = File.ReadAllText(ProductionPubSubKafkaPath);
        kafkaContent.ShouldContain("scopes:", customMessage:
            "Production Kafka pub/sub must have component-level scopes (AC #7)");
        kafkaContent.ShouldContain("commandapi", customMessage:
            "Production Kafka pub/sub scopes must include commandapi");
    }

    // --- Task 5.9: State store scoping validation ---

    [Fact]
    public void StateStoreYaml_HasScopes_RestrictsAppAccess()
    {
        // Verify local state store has scopes
        string localContent = File.ReadAllText(LocalStateStorePath);
        localContent.ShouldContain("scopes:", customMessage:
            "Local state store must have component-level scopes (AC #2, AC #4)");
        localContent.ShouldContain("commandapi", customMessage:
            "Local state store scopes must include commandapi");

        // Verify production state store configs have scopes
        string postgresContent = File.ReadAllText(ProductionStateStorePostgresPath);
        postgresContent.ShouldContain("scopes:", customMessage:
            "Production PostgreSQL state store must have component-level scopes (AC #2, AC #4)");
        postgresContent.ShouldContain("commandapi", customMessage:
            "Production PostgreSQL state store scopes must include commandapi");

        string cosmosContent = File.ReadAllText(ProductionStateStoreCosmosPath);
        cosmosContent.ShouldContain("scopes:", customMessage:
            "Production Cosmos DB state store must have component-level scopes (AC #2, AC #4)");
        cosmosContent.ShouldContain("commandapi", customMessage:
            "Production Cosmos DB state store scopes must include commandapi");
    }

    // --- Task 5.10: Local-production topology consistency ---

    [Fact]
    public void AllDaprComponents_LocalAndProduction_PolicyTopologyConsistent()
    {
        string localAc = File.ReadAllText(LocalAccessControlPath);
        string prodAc = File.ReadAllText(ProductionAccessControlPath);

        // Both must have deny-by-default
        localAc.ShouldContain("defaultAction: deny");
        prodAc.ShouldContain("defaultAction: deny");

        // Both must have commandapi policy with allowed operations
        localAc.ShouldContain("appId: commandapi");
        prodAc.ShouldContain("appId: commandapi");

        // Both must allow POST invocations with wildcard path
        localAc.ShouldContain("/**");
        prodAc.ShouldContain("/**");
        localAc.ShouldContain("action: allow");
        prodAc.ShouldContain("action: allow");

        // Both must have the same trust domain
        localAc.ShouldContain("trustDomain: \"hexalith.io\"");
        prodAc.ShouldContain("trustDomain: \"hexalith.io\"");

        // Both pub/sub configs must restrict to commandapi
        string localPubSub = File.ReadAllText(LocalPubSubPath);
        string prodRabbit = File.ReadAllText(ProductionPubSubRabbitMqPath);
        string prodKafka = File.ReadAllText(ProductionPubSubKafkaPath);

        VerifyComponentScopedToCommandApi(localPubSub, "local pub/sub");
        VerifyComponentScopedToCommandApi(prodRabbit, "production RabbitMQ pub/sub");
        VerifyComponentScopedToCommandApi(prodKafka, "production Kafka pub/sub");

        // Both state store configs must restrict to commandapi
        string localState = File.ReadAllText(LocalStateStorePath);
        string prodPostgres = File.ReadAllText(ProductionStateStorePostgresPath);
        string prodCosmos = File.ReadAllText(ProductionStateStoreCosmosPath);

        VerifyComponentScopedToCommandApi(localState, "local state store");
        VerifyComponentScopedToCommandApi(prodPostgres, "production PostgreSQL state store");
        VerifyComponentScopedToCommandApi(prodCosmos, "production Cosmos DB state store");
    }

    // --- Task 5.11: mTLS trust domain configuration ---

    [Fact]
    public void LocalAccessControlYaml_HasTrustDomain_MtlsConfigured()
    {
        string content = File.ReadAllText(LocalAccessControlPath);

        content.ShouldContain("trustDomain:", customMessage:
            "Local access control must configure a trust domain for mTLS SPIFFE identity (AC #11)");
        content.ShouldContain("hexalith.io", customMessage:
            "Local access control trust domain must be hexalith.io");
    }

    // --- Task 5.12: Dead-letter topic scoping ---

    [Fact]
    public void PubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly()
    {
        // Dead-letter topics are protected by component-level scoping:
        // only commandapi has access to the pub/sub component, so only commandapi
        // can publish to dead-letter topics (AC #12).
        string localContent = File.ReadAllText(LocalPubSubPath);

        // Verify dead-letter is enabled
        localContent.ShouldContain("enableDeadLetter", customMessage:
            "Local pub/sub must have dead-letter enabled");
        localContent.ShouldContain("deadLetterTopic", customMessage:
            "Local pub/sub must configure a dead-letter topic");

        // Verify component scopes restrict to commandapi only (which implicitly
        // restricts dead-letter topic access to commandapi)
        VerifyComponentScopedToCommandApi(localContent, "local pub/sub (dead-letter check)");

        // Verify sample is explicitly denied publishing access
        localContent.ShouldContain("publishingScopes", customMessage:
            "Local pub/sub must have publishingScopes for defense-in-depth (AC #12)");
        localContent.ShouldContain("sample=", customMessage:
            "Local pub/sub publishingScopes must explicitly deny sample publishing access");
    }

    // --- Production dead-letter scoping (AC #12 consistency) ---

    [Fact]
    public void ProductionPubSubYaml_DeadLetterTopics_ScopedToCommandApiOnly()
    {
        // AC #12: dead-letter scoping must be consistent across local and production.
        // Production configs restrict dead-letter access via component-level scoping.
        string rabbitContent = File.ReadAllText(ProductionPubSubRabbitMqPath);
        rabbitContent.ShouldContain("enableDeadLetter", customMessage:
            "Production RabbitMQ pub/sub must have dead-letter enabled");
        rabbitContent.ShouldContain("deadLetterTopic", customMessage:
            "Production RabbitMQ pub/sub must configure a dead-letter topic");
        VerifyComponentScopedToCommandApi(rabbitContent, "production RabbitMQ pub/sub (dead-letter check)");

        string kafkaContent = File.ReadAllText(ProductionPubSubKafkaPath);
        kafkaContent.ShouldContain("enableDeadLetter", customMessage:
            "Production Kafka pub/sub must have dead-letter enabled");
        kafkaContent.ShouldContain("deadLetterTopic", customMessage:
            "Production Kafka pub/sub must configure a dead-letter topic");
        VerifyComponentScopedToCommandApi(kafkaContent, "production Kafka pub/sub (dead-letter check)");
    }

    // --- Namespace configuration validation ---

    [Fact]
    public void AccessControlYaml_HasNamespace_IdentityConfigured()
    {
        string localContent = File.ReadAllText(LocalAccessControlPath);
        localContent.ShouldContain("namespace:", customMessage:
            "Local access control must configure namespace for SPIFFE identity scoping");
        // Verify namespace has a non-empty value (not just "namespace:" or "namespace: ")
        localContent.ShouldContain("namespace: \"default\"", customMessage:
            "Local access control namespace must be 'default' for local development");

        string prodContent = File.ReadAllText(ProductionAccessControlPath);
        prodContent.ShouldContain("namespace:", customMessage:
            "Production access control must configure namespace for SPIFFE identity scoping");
        prodContent.ShouldContain("namespace: \"hexalith\"", customMessage:
            "Production access control namespace must be 'hexalith' (production Kubernetes namespace)");
    }

    // --- Task 5.13: Domain service zero-infrastructure-access ---

    [Fact]
    public void DomainServicePolicy_ZeroInfrastructureAccess_AllDenied()
    {
        // Verify domain services (sample) have zero access to ALL infrastructure:
        // no state store, no pub/sub, no outbound service invocation (AC #13).

        // 1. Access control: sample has defaultAction: deny with no allowed operations
        string acContent = File.ReadAllText(LocalAccessControlPath);
        int samplePolicyStart = acContent.IndexOf("appId: sample", StringComparison.Ordinal);
        samplePolicyStart.ShouldBeGreaterThan(-1, "Access control must contain sample policy");
        string sampleSection = acContent[samplePolicyStart..];
        sampleSection.ShouldContain("defaultAction: deny", customMessage:
            "sample must have defaultAction: deny in access control");
        sampleSection.ShouldNotContain("action: allow", customMessage:
            "sample must not have any allowed service invocation operations (AC #13)");

        // 2. Pub/sub: sample is excluded from component scopes
        string pubSubContent = File.ReadAllText(LocalPubSubPath);
        VerifySampleExcludedFromScopes(pubSubContent, "pub/sub");

        // 3. State store: sample is excluded from component scopes
        string stateContent = File.ReadAllText(LocalStateStorePath);
        VerifySampleExcludedFromScopes(stateContent, "state store");

        // 4. Pub/sub topic scoping: sample explicitly denied publishing and subscription
        pubSubContent.ShouldContain("publishingScopes", customMessage:
            "Pub/sub must have publishingScopes restricting sample");
        pubSubContent.ShouldContain("subscriptionScopes", customMessage:
            "Pub/sub must have subscriptionScopes restricting sample");

        // 5. Production configs: scopes must contain ONLY commandapi (no unauthorized app-ids)
        string prodPubSubRabbit = File.ReadAllText(ProductionPubSubRabbitMqPath);
        VerifyComponentScopedToCommandApi(prodPubSubRabbit, "production RabbitMQ pub/sub");
        VerifyScopesContainOnlyCommandApi(prodPubSubRabbit, "production RabbitMQ pub/sub");

        string prodPubSubKafka = File.ReadAllText(ProductionPubSubKafkaPath);
        VerifyComponentScopedToCommandApi(prodPubSubKafka, "production Kafka pub/sub");
        VerifyScopesContainOnlyCommandApi(prodPubSubKafka, "production Kafka pub/sub");

        string prodStatePostgres = File.ReadAllText(ProductionStateStorePostgresPath);
        VerifyComponentScopedToCommandApi(prodStatePostgres, "production PostgreSQL state store");
        VerifyScopesContainOnlyCommandApi(prodStatePostgres, "production PostgreSQL state store");

        string prodStateCosmos = File.ReadAllText(ProductionStateStoreCosmosPath);
        VerifyComponentScopedToCommandApi(prodStateCosmos, "production Cosmos DB state store");
        VerifyScopesContainOnlyCommandApi(prodStateCosmos, "production Cosmos DB state store");
    }

    // --- Helper methods ---

    private static void VerifyComponentScopedToCommandApi(string content, string componentName)
    {
        content.ShouldContain("scopes:", customMessage:
            $"{componentName} must have component-level scopes");
        content.ShouldContain("commandapi", customMessage:
            $"{componentName} scopes must include commandapi");
    }

    private static void VerifyScopesContainOnlyCommandApi(string content, string componentName)
    {
        // Extract lines in the scopes section and verify no unauthorized app-ids.
        // Stop at the next top-level YAML key (non-indented, non-comment, non-list line)
        // to avoid false matches against list items in subsequent sections.
        int scopesStart = content.IndexOf("scopes:", StringComparison.Ordinal);
        scopesStart.ShouldBeGreaterThan(-1, $"{componentName} must have a scopes section");
        string scopesSection = content[scopesStart..];

        string[] lines = scopesSection.Split('\n');
        bool foundEntries = false;
        for (int i = 1; i < lines.Length; i++) // skip the "scopes:" line itself
        {
            string trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            // A non-indented, non-list line means we've left the scopes block
            if (!lines[i].StartsWith(' ') && !lines[i].StartsWith('\t') && !trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                string appId = trimmed[2..].Trim();
                appId.ShouldBe("commandapi",
                    $"{componentName} scopes must contain ONLY commandapi, found '{appId}'");
                foundEntries = true;
            }
        }

        foundEntries.ShouldBeTrue($"{componentName} scopes section must have at least one entry");
    }

    private static void VerifySampleExcludedFromScopes(string content, string componentName)
    {
        // Extract the scopes section (from "scopes:" to end of file)
        int scopesStart = content.IndexOf("scopes:", StringComparison.Ordinal);
        scopesStart.ShouldBeGreaterThan(-1, $"{componentName} must have a scopes section");
        string scopesSection = content[scopesStart..];

        // scopes should contain commandapi but NOT sample
        scopesSection.ShouldContain("commandapi", customMessage:
            $"{componentName} scopes must include commandapi");
        scopesSection.ShouldNotContain("sample", customMessage:
            $"{componentName} scopes must NOT include sample -- zero-trust posture (AC #13)");
    }
}
