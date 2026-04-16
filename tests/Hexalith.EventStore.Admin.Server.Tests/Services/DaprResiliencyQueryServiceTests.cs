using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Server.Services;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprResiliencyQueryServiceTests {
    private const string ProductionYaml = """
        apiVersion: dapr.io/v1alpha1
        kind: Resiliency
        metadata:
          name: resiliency
        spec:
          policies:
            retries:
              defaultRetry:
                policy: exponential
                maxInterval: 15s
                maxRetries: 10
              pubsubRetryOutbound:
                policy: exponential
                maxInterval: 10s
                maxRetries: 5
              pubsubRetryInbound:
                policy: exponential
                maxInterval: 60s
                maxRetries: 20
            timeouts:
              daprSidecar:
                general: 5s
              pubsubTimeout: 10s
              subscriberTimeout: 30s
            circuitBreakers:
              defaultBreaker:
                maxRequests: 1
                interval: 60s
                timeout: 60s
                trip: consecutiveFailures > 5
              pubsubBreaker:
                maxRequests: 1
                interval: 60s
                timeout: 60s
                trip: consecutiveFailures > 10
          targets:
            apps:
              eventstore:
                retry: defaultRetry
                timeout: daprSidecar
                circuitBreaker: defaultBreaker
            components:
              pubsub:
                outbound:
                  retry: pubsubRetryOutbound
                  timeout: pubsubTimeout
                  circuitBreaker: pubsubBreaker
                inbound:
                  retry: pubsubRetryInbound
                  timeout: subscriberTimeout
              statestore:
                retry: defaultRetry
                timeout: daprSidecar
                circuitBreaker: defaultBreaker
        """;

    [Fact]
    public void ParseResiliencyYaml_ProductionFixture_ParsesAllPoliciesAndTargets() {
        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(ProductionYaml);

        // Retry policies
        result.RetryPolicies.Count.ShouldBe(3);
        result.RetryPolicies[0].Name.ShouldBe("defaultRetry");
        result.RetryPolicies[0].Strategy.ShouldBe("exponential");
        result.RetryPolicies[0].MaxRetries.ShouldBe(10);
        result.RetryPolicies[0].MaxInterval.ShouldBe("15s");
        result.RetryPolicies[0].Duration.ShouldBeNull();

        result.RetryPolicies[1].Name.ShouldBe("pubsubRetryOutbound");
        result.RetryPolicies[1].MaxRetries.ShouldBe(5);
        result.RetryPolicies[1].MaxInterval.ShouldBe("10s");

        result.RetryPolicies[2].Name.ShouldBe("pubsubRetryInbound");
        result.RetryPolicies[2].MaxRetries.ShouldBe(20);
        result.RetryPolicies[2].MaxInterval.ShouldBe("60s");

        // Timeout policies
        result.TimeoutPolicies.Count.ShouldBe(3);
        result.TimeoutPolicies[0].Name.ShouldBe("daprSidecar");
        result.TimeoutPolicies[0].Value.ShouldBe("5s");
        result.TimeoutPolicies[1].Name.ShouldBe("pubsubTimeout");
        result.TimeoutPolicies[1].Value.ShouldBe("10s");
        result.TimeoutPolicies[2].Name.ShouldBe("subscriberTimeout");
        result.TimeoutPolicies[2].Value.ShouldBe("30s");

        // Circuit breaker policies
        result.CircuitBreakerPolicies.Count.ShouldBe(2);
        result.CircuitBreakerPolicies[0].Name.ShouldBe("defaultBreaker");
        result.CircuitBreakerPolicies[0].MaxRequests.ShouldBe(1);
        result.CircuitBreakerPolicies[0].Interval.ShouldBe("60s");
        result.CircuitBreakerPolicies[0].Timeout.ShouldBe("60s");
        result.CircuitBreakerPolicies[0].Trip.ShouldBe("consecutiveFailures > 5");

        result.CircuitBreakerPolicies[1].Name.ShouldBe("pubsubBreaker");
        result.CircuitBreakerPolicies[1].Trip.ShouldBe("consecutiveFailures > 10");

        // Target bindings (sorted: App before Component)
        result.TargetBindings.Count.ShouldBe(4);

        result.IsConfigurationAvailable.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        _ = result.RawYamlContent.ShouldNotBeNull();
    }

    [Fact]
    public void ParseResiliencyYaml_EmptySpec_ReturnsEmptyPoliciesAndTargets() {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies: {}
              targets: {}
            """;

        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(yaml);

        result.RetryPolicies.ShouldBeEmpty();
        result.TimeoutPolicies.ShouldBeEmpty();
        result.CircuitBreakerPolicies.ShouldBeEmpty();
        result.TargetBindings.ShouldBeEmpty();
        result.IsConfigurationAvailable.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void ParseResiliencyYaml_OnlyRetries_ReturnsRetriesWithEmptyTimeoutsAndBreakers() {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                retries:
                  myRetry:
                    policy: constant
                    duration: 2s
                    maxRetries: 3
            """;

        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(yaml);

        result.RetryPolicies.Count.ShouldBe(1);
        result.RetryPolicies[0].Name.ShouldBe("myRetry");
        result.RetryPolicies[0].Strategy.ShouldBe("constant");
        result.RetryPolicies[0].Duration.ShouldBe("2s");
        result.RetryPolicies[0].MaxRetries.ShouldBe(3);
        result.TimeoutPolicies.ShouldBeEmpty();
        result.CircuitBreakerPolicies.ShouldBeEmpty();
    }

    [Fact]
    public void ParseResiliencyYaml_MixedTimeoutForms_ParsesBothStringAndNested() {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                timeouts:
                  sidecarTimeout:
                    general: 5s
                  simpleTimeout: 10s
            """;

        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(yaml);

        result.TimeoutPolicies.Count.ShouldBe(2);
        result.TimeoutPolicies[0].Name.ShouldBe("sidecarTimeout");
        result.TimeoutPolicies[0].Value.ShouldBe("5s");
        result.TimeoutPolicies[1].Name.ShouldBe("simpleTimeout");
        result.TimeoutPolicies[1].Value.ShouldBe("10s");
    }

    [Fact]
    public void ParseResiliencyYaml_DirectionalComponent_ProducesTwoBindings() {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              policies:
                retries:
                  retryOut:
                    policy: exponential
                    maxInterval: 10s
                    maxRetries: 5
                  retryIn:
                    policy: exponential
                    maxInterval: 60s
                    maxRetries: 20
              targets:
                components:
                  pubsub:
                    outbound:
                      retry: retryOut
                      timeout: outTimeout
                    inbound:
                      retry: retryIn
                      timeout: inTimeout
            """;

        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(yaml);

        result.TargetBindings.Count.ShouldBe(2);

        DaprResiliencyTargetBinding outbound = result.TargetBindings.First(b => b.Direction == "Outbound");
        outbound.TargetName.ShouldBe("pubsub");
        outbound.TargetType.ShouldBe("Component");
        outbound.RetryPolicy.ShouldBe("retryOut");
        outbound.TimeoutPolicy.ShouldBe("outTimeout");

        DaprResiliencyTargetBinding inbound = result.TargetBindings.First(b => b.Direction == "Inbound");
        inbound.TargetName.ShouldBe("pubsub");
        inbound.TargetType.ShouldBe("Component");
        inbound.RetryPolicy.ShouldBe("retryIn");
        inbound.TimeoutPolicy.ShouldBe("inTimeout");
    }

    [Fact]
    public void ParseResiliencyYaml_NonDirectionalComponent_ProducesOneBindingWithNullDirection() {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              targets:
                components:
                  statestore:
                    retry: defaultRetry
                    timeout: daprSidecar
                    circuitBreaker: defaultBreaker
            """;

        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(yaml);

        result.TargetBindings.Count.ShouldBe(1);
        result.TargetBindings[0].TargetName.ShouldBe("statestore");
        result.TargetBindings[0].TargetType.ShouldBe("Component");
        result.TargetBindings[0].Direction.ShouldBeNull();
        result.TargetBindings[0].RetryPolicy.ShouldBe("defaultRetry");
        result.TargetBindings[0].TimeoutPolicy.ShouldBe("daprSidecar");
        result.TargetBindings[0].CircuitBreakerPolicy.ShouldBe("defaultBreaker");
    }

    [Fact]
    public void ParseResiliencyYaml_AppTarget_ProducesOneBindingWithAppTypeAndNullDirection() {
        const string yaml = """
            apiVersion: dapr.io/v1alpha1
            kind: Resiliency
            metadata:
              name: resiliency
            spec:
              targets:
                apps:
                  eventstore:
                    retry: defaultRetry
                    timeout: daprSidecar
                    circuitBreaker: defaultBreaker
            """;

        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(yaml);

        result.TargetBindings.Count.ShouldBe(1);
        result.TargetBindings[0].TargetName.ShouldBe("eventstore");
        result.TargetBindings[0].TargetType.ShouldBe("App");
        result.TargetBindings[0].Direction.ShouldBeNull();
        result.TargetBindings[0].RetryPolicy.ShouldBe("defaultRetry");
        result.TargetBindings[0].TimeoutPolicy.ShouldBe("daprSidecar");
        result.TargetBindings[0].CircuitBreakerPolicy.ShouldBe("defaultBreaker");
    }

    [Fact]
    public void ParseResiliencyYaml_ProductionFixture_ProducesExactFourTargetRows() {
        DaprResiliencySpec result = DaprInfrastructureQueryService.ParseResiliencyYaml(ProductionYaml);

        // Sorted: App targets first, then Component targets (alphabetical by type then name)
        result.TargetBindings.Count.ShouldBe(4);

        // Row 1: App - eventstore
        result.TargetBindings[0].TargetName.ShouldBe("eventstore");
        result.TargetBindings[0].TargetType.ShouldBe("App");
        result.TargetBindings[0].Direction.ShouldBeNull();
        result.TargetBindings[0].RetryPolicy.ShouldBe("defaultRetry");
        result.TargetBindings[0].TimeoutPolicy.ShouldBe("daprSidecar");
        result.TargetBindings[0].CircuitBreakerPolicy.ShouldBe("defaultBreaker");

        // Row 2: Component - pubsub (Outbound)
        result.TargetBindings[1].TargetName.ShouldBe("pubsub");
        result.TargetBindings[1].TargetType.ShouldBe("Component");
        result.TargetBindings[1].Direction.ShouldBe("Outbound");
        result.TargetBindings[1].RetryPolicy.ShouldBe("pubsubRetryOutbound");
        result.TargetBindings[1].TimeoutPolicy.ShouldBe("pubsubTimeout");
        result.TargetBindings[1].CircuitBreakerPolicy.ShouldBe("pubsubBreaker");

        // Row 3: Component - pubsub (Inbound)
        result.TargetBindings[2].TargetName.ShouldBe("pubsub");
        result.TargetBindings[2].TargetType.ShouldBe("Component");
        result.TargetBindings[2].Direction.ShouldBe("Inbound");
        result.TargetBindings[2].RetryPolicy.ShouldBe("pubsubRetryInbound");
        result.TargetBindings[2].TimeoutPolicy.ShouldBe("subscriberTimeout");
        result.TargetBindings[2].CircuitBreakerPolicy.ShouldBeNull();

        // Row 4: Component - statestore
        result.TargetBindings[3].TargetName.ShouldBe("statestore");
        result.TargetBindings[3].TargetType.ShouldBe("Component");
        result.TargetBindings[3].Direction.ShouldBeNull();
        result.TargetBindings[3].RetryPolicy.ShouldBe("defaultRetry");
        result.TargetBindings[3].TimeoutPolicy.ShouldBe("daprSidecar");
        result.TargetBindings[3].CircuitBreakerPolicy.ShouldBe("defaultBreaker");
    }

    [Fact]
    public void ParseResiliencyYaml_InvalidYaml_ThrowsException() {
        const string invalidYaml = """
            not: valid: yaml: {{{{
            - broken
              indentation
            """;

        _ = Should.Throw<Exception>(() => DaprInfrastructureQueryService.ParseResiliencyYaml(invalidYaml));
    }
}
