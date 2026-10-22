
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;
/// <summary>
/// Story 4.3 Task 6: DAPR resiliency configuration validation tests.
/// Parses resiliency.yaml files to verify pub/sub retry, circuit breaker, and timeout policies exist.
/// </summary>
public class ResiliencyConfigurationTests {
    private static readonly string LocalResiliencyPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.AppHost", "DaprComponents", "resiliency.yaml"));

    private static readonly string ProductionResiliencyPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "deploy", "dapr", "resiliency.yaml"));

    // --- Task 6.2: Local pubsubRetryOutbound ---

    [Fact]
    public void LocalResiliency_ContainsPubSubOutboundRetryPolicy() {
        string content = File.ReadAllText(LocalResiliencyPath);

        content.ShouldContain("pubsubRetryOutbound:");
        content.ShouldContain("policy: exponential");
        content.ShouldContain("maxRetries: 3", customMessage:
            "Local outbound retry should be conservative (3) -- Story 4.4 recovery handles prolonged outages");
    }

    // --- Task 6.3: Local pubsubRetryInbound ---

    [Fact]
    public void LocalResiliency_ContainsPubSubInboundRetryPolicy() {
        string content = File.ReadAllText(LocalResiliencyPath);

        content.ShouldContain("pubsubRetryInbound:");
        content.ShouldContain("maxRetries: 10", customMessage:
            "Local inbound retry should allow 10 retries for subscriber processing failures");
    }

    // --- Task 6.4: Local pubsubBreaker ---

    [Fact]
    public void LocalResiliency_ContainsPubSubCircuitBreaker() {
        string content = File.ReadAllText(LocalResiliencyPath);

        content.ShouldContain("pubsubBreaker:");
        content.ShouldContain("consecutiveFailures > 5");
        content.ShouldContain("maxRequests: 1", customMessage:
            "Circuit breaker should use single probe in half-open state");
    }

    // --- Task 6.5: Local targets.components.pubsub ---

    [Fact]
    public void LocalResiliency_TargetsPubSubComponent() {
        string content = File.ReadAllText(LocalResiliencyPath);

        content.ShouldContain("components:");
        content.ShouldContain("pubsub:");
        content.ShouldContain("outbound:");
        content.ShouldContain("inbound:");
        content.ShouldContain("retry: pubsubRetryOutbound");
        content.ShouldContain("retry: pubsubRetryInbound");
        content.ShouldContain("circuitBreaker: pubsubBreaker", customMessage:
            "Local pub/sub outbound target must reference pubsubBreaker for fast-fail behavior");
    }

    [Fact]
    public void ProductionResiliency_TargetsPubSubCircuitBreaker() {
        string content = File.ReadAllText(ProductionResiliencyPath);

        content.ShouldContain("components:");
        content.ShouldContain("pubsub:");
        content.ShouldContain("outbound:");
        content.ShouldContain("circuitBreaker: pubsubBreaker", customMessage:
            "Production pub/sub outbound target must reference pubsubBreaker for fast-fail behavior");
    }

    // --- Task 6.6: Production pub/sub retry policies ---

    [Fact]
    public void ProductionResiliency_ContainsPubSubRetryPolicies() {
        string content = File.ReadAllText(ProductionResiliencyPath);

        content.ShouldContain("pubsubRetryOutbound:");
        content.ShouldContain("pubsubRetryInbound:");
        content.ShouldContain("pubsubBreaker:");
        content.ShouldContain("components:");
    }

    // --- Task 6.7: Production higher retry limits ---

    [Fact]
    public void ProductionResiliency_HigherRetryLimits() {
        string localContent = File.ReadAllText(LocalResiliencyPath);
        string prodContent = File.ReadAllText(ProductionResiliencyPath);

        // Extract outbound maxRetries
        int localOutbound = ExtractMaxRetries(localContent, "pubsubRetryOutbound");
        int prodOutbound = ExtractMaxRetries(prodContent, "pubsubRetryOutbound");

        // Extract inbound maxRetries
        int localInbound = ExtractMaxRetries(localContent, "pubsubRetryInbound");
        int prodInbound = ExtractMaxRetries(prodContent, "pubsubRetryInbound");

        prodOutbound.ShouldBeGreaterThan(localOutbound,
            $"Production outbound retries ({prodOutbound}) should exceed local ({localOutbound})");
        prodInbound.ShouldBeGreaterThan(localInbound,
            $"Production inbound retries ({prodInbound}) should exceed local ({localInbound})");
    }

    // --- Task 6.8: Local timeout policies ---

    [Fact]
    public void LocalResiliency_ContainsPubSubTimeoutPolicy() {
        string content = File.ReadAllText(LocalResiliencyPath);

        content.ShouldContain("pubsubTimeout: 10s", customMessage:
            "Outbound timeout should be 10s to prevent hung sidecar->broker calls");
        content.ShouldContain("subscriberTimeout: 30s", customMessage:
            "Inbound timeout should be 30s for subscriber processing");
    }

    // --- Task 6.9: Production timeout policies ---

    [Fact]
    public void ProductionResiliency_ContainsPubSubTimeoutPolicy() {
        string content = File.ReadAllText(ProductionResiliencyPath);

        content.ShouldContain("pubsubTimeout:");
        content.ShouldContain("subscriberTimeout:");
    }

    // --- Task 6.10: Cross-reference validation ---

    [Fact]
    public void Resiliency_AllTargetPolicyNamesExistInPolicies() {
        ValidatePolicyReferences(LocalResiliencyPath, "local");
        ValidatePolicyReferences(ProductionResiliencyPath, "production");
    }

    [Fact]
    public void LocalResiliency_PreservesExistingAppTargets() {
        string content = File.ReadAllText(LocalResiliencyPath);

        // Verify existing eventstore config is preserved
        content.ShouldContain("apps:");
        content.ShouldContain("eventstore:");
        content.ShouldContain("retry: defaultRetry");
        content.ShouldContain("timeout: daprSidecar");
        content.ShouldContain("circuitBreaker: defaultBreaker");
    }

    [Fact]
    public void LocalResiliency_DocumentsAugmentationBehavior() {
        string content = File.ReadAllText(LocalResiliencyPath);

        content.ShouldContain("AUGMENT", customMessage:
            "Resiliency YAML should document that policies AUGMENT built-in component retries");
    }

    private static void ValidatePolicyReferences(string yamlPath, string environment) {
        string content = File.ReadAllText(yamlPath);
        string[] lines = content.Split('\n');

        // Collect all defined policy names from policies section
        var definedRetries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definedTimeouts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var definedBreakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool inRetries = false;
        bool inTimeouts = false;
        bool inBreakers = false;

        foreach (string rawLine in lines) {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();

            // Track which section we're in (retries, timeouts, circuitBreakers under policies)
            if (trimmed.StartsWith("retries:", StringComparison.Ordinal)) {
                inRetries = true; inTimeouts = false; inBreakers = false;
                continue;
            }

            if (trimmed.StartsWith("timeouts:", StringComparison.Ordinal)) {
                inRetries = false; inTimeouts = true; inBreakers = false;
                continue;
            }

            if (trimmed.StartsWith("circuitBreakers:", StringComparison.Ordinal)) {
                inRetries = false; inTimeouts = false; inBreakers = true;
                continue;
            }

            if (trimmed.StartsWith("targets:", StringComparison.Ordinal)) {
                break; // Done collecting policy names
            }

            // Collect policy names: both block-style "name:" and inline "name: value"
            if (!trimmed.StartsWith('#') && !trimmed.StartsWith('-') && trimmed.Contains(':')) {
                int colonIndex = trimmed.IndexOf(':');
                string name = trimmed[..colonIndex].Trim();
                if (string.IsNullOrEmpty(name) || name.Contains(' ')) {
                    continue;
                }

                // Skip known sub-properties that aren't policy names
                string[] subProperties = ["policy", "duration", "maxInterval", "maxRetries",
                    "general", "maxRequests", "interval", "timeout", "trip"];
                if (Array.Exists(subProperties, p => p.Equals(name, StringComparison.OrdinalIgnoreCase))) {
                    continue;
                }

                if (inRetries) {
                    _ = definedRetries.Add(name);
                }
                else if (inTimeouts) {
                    _ = definedTimeouts.Add(name);
                }
                else if (inBreakers) {
                    _ = definedBreakers.Add(name);
                }
            }
        }

        // Now check all target references
        bool inTargets = false;
        foreach (string rawLine in lines) {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("targets:", StringComparison.Ordinal)) {
                inTargets = true;
                continue;
            }

            if (!inTargets || trimmed.StartsWith('#')) {
                continue;
            }

            // Check retry references
            if (trimmed.StartsWith("retry:", StringComparison.Ordinal)) {
                string policyName = trimmed["retry:".Length..].Trim();
                definedRetries.ShouldContain(policyName,
                    $"[{environment}] Target references retry policy '{policyName}' which is not defined in policies.retries section");
            }

            // Check timeout references
            if (trimmed.StartsWith("timeout:", StringComparison.Ordinal)) {
                string policyName = trimmed["timeout:".Length..].Trim();
                definedTimeouts.ShouldContain(policyName,
                    $"[{environment}] Target references timeout policy '{policyName}' which is not defined in policies.timeouts section");
            }

            // Check circuit breaker references
            if (trimmed.StartsWith("circuitBreaker:", StringComparison.Ordinal)) {
                string policyName = trimmed["circuitBreaker:".Length..].Trim();
                definedBreakers.ShouldContain(policyName,
                    $"[{environment}] Target references circuit breaker '{policyName}' which is not defined in policies.circuitBreakers section");
            }
        }
    }

    private static int ExtractMaxRetries(string yamlContent, string policyName) {
        string[] lines = yamlContent.Split('\n');
        bool inPolicy = false;

        foreach (string rawLine in lines) {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith($"{policyName}:", StringComparison.Ordinal)) {
                inPolicy = true;
                continue;
            }

            if (inPolicy && trimmed.StartsWith("maxRetries:", StringComparison.Ordinal)) {
                string value = trimmed["maxRetries:".Length..].Trim();
                return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            }

            // If we hit another policy definition, stop looking
            if (inPolicy && !trimmed.StartsWith('#') && trimmed.EndsWith(':') && !trimmed.StartsWith("maxR") && !trimmed.StartsWith("max") && !trimmed.StartsWith("policy") && !trimmed.StartsWith("duration")) {
                break;
            }
        }

        throw new InvalidOperationException($"Could not find maxRetries for policy '{policyName}'");
    }
}
