using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprResiliencySpecTests
{
    [Fact]
    public void Constructor_WithValidEmptyCollections_CreatesInstance()
    {
        var spec = new DaprResiliencySpec(
            [],
            [],
            [],
            [],
            IsConfigurationAvailable: true,
            RawYamlContent: "apiVersion: dapr.io/v1alpha1",
            ErrorMessage: null);

        spec.RetryPolicies.ShouldBeEmpty();
        spec.TimeoutPolicies.ShouldBeEmpty();
        spec.CircuitBreakerPolicies.ShouldBeEmpty();
        spec.TargetBindings.ShouldBeEmpty();
        spec.IsConfigurationAvailable.ShouldBeTrue();
        spec.RawYamlContent.ShouldBe("apiVersion: dapr.io/v1alpha1");
        spec.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNullRetryPolicies_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprResiliencySpec(
                null!,
                [],
                [],
                [],
                IsConfigurationAvailable: true,
                RawYamlContent: null,
                ErrorMessage: null));
    }

    [Fact]
    public void Constructor_WithNullTimeoutPolicies_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprResiliencySpec(
                [],
                null!,
                [],
                [],
                IsConfigurationAvailable: true,
                RawYamlContent: null,
                ErrorMessage: null));
    }

    [Fact]
    public void Constructor_WithNullCircuitBreakerPolicies_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprResiliencySpec(
                [],
                [],
                null!,
                [],
                IsConfigurationAvailable: true,
                RawYamlContent: null,
                ErrorMessage: null));
    }

    [Fact]
    public void Constructor_WithNullTargetBindings_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DaprResiliencySpec(
                [],
                [],
                [],
                null!,
                IsConfigurationAvailable: true,
                RawYamlContent: null,
                ErrorMessage: null));
    }

    [Fact]
    public void Unavailable_ReturnsSpecWithConfigurationNotAvailable()
    {
        DaprResiliencySpec spec = DaprResiliencySpec.Unavailable;

        spec.IsConfigurationAvailable.ShouldBeFalse();
        spec.RetryPolicies.ShouldBeEmpty();
        spec.TimeoutPolicies.ShouldBeEmpty();
        spec.CircuitBreakerPolicies.ShouldBeEmpty();
        spec.TargetBindings.ShouldBeEmpty();
        spec.RawYamlContent.ShouldBeNull();
        spec.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void NotFound_ReturnsSpecWithConfigurationNotAvailable()
    {
        DaprResiliencySpec spec = DaprResiliencySpec.NotFound("/etc/dapr/resiliency.yaml");

        spec.IsConfigurationAvailable.ShouldBeFalse();
        spec.RetryPolicies.ShouldBeEmpty();
        spec.TimeoutPolicies.ShouldBeEmpty();
        spec.CircuitBreakerPolicies.ShouldBeEmpty();
        spec.TargetBindings.ShouldBeEmpty();
        spec.RawYamlContent.ShouldBeNull();
        spec.ErrorMessage.ShouldNotBeNull();
        spec.ErrorMessage.ShouldContain("/etc/dapr/resiliency.yaml");
        spec.ErrorMessage.ShouldContain("not found");
    }

    [Fact]
    public void ReadError_ReturnsSpecWithConfigurationNotAvailable()
    {
        DaprResiliencySpec spec = DaprResiliencySpec.ReadError(
            "/etc/dapr/resiliency.yaml",
            "Permission denied");

        spec.IsConfigurationAvailable.ShouldBeFalse();
        spec.RetryPolicies.ShouldBeEmpty();
        spec.TimeoutPolicies.ShouldBeEmpty();
        spec.CircuitBreakerPolicies.ShouldBeEmpty();
        spec.TargetBindings.ShouldBeEmpty();
        spec.RawYamlContent.ShouldBeNull();
        spec.ErrorMessage.ShouldNotBeNull();
        spec.ErrorMessage.ShouldContain("/etc/dapr/resiliency.yaml");
        spec.ErrorMessage.ShouldContain("Permission denied");
    }

    [Fact]
    public void ParseError_ReturnsSpecWithConfigurationNotAvailableAndPreservesRawYaml()
    {
        const string rawYaml = "apiVersion: dapr.io/v1alpha1\nkind: Resiliency\ninvalid: yaml: content";

        DaprResiliencySpec spec = DaprResiliencySpec.ParseError(
            "/etc/dapr/resiliency.yaml",
            rawYaml,
            "Invalid YAML at line 3");

        spec.IsConfigurationAvailable.ShouldBeFalse();
        spec.RetryPolicies.ShouldBeEmpty();
        spec.TimeoutPolicies.ShouldBeEmpty();
        spec.CircuitBreakerPolicies.ShouldBeEmpty();
        spec.TargetBindings.ShouldBeEmpty();
        spec.RawYamlContent.ShouldBe(rawYaml);
        spec.ErrorMessage.ShouldNotBeNull();
        spec.ErrorMessage.ShouldContain("/etc/dapr/resiliency.yaml");
        spec.ErrorMessage.ShouldContain("Invalid YAML at line 3");
    }

    [Fact]
    public void Constructor_WithPopulatedCollections_CreatesInstance()
    {
        var retryPolicies = new List<DaprRetryPolicy>
        {
            new("defaultRetry", "constant", 3, "1s", null),
        };
        var timeoutPolicies = new List<DaprTimeoutPolicy>
        {
            new("defaultTimeout", "5s"),
        };
        var circuitBreakerPolicies = new List<DaprCircuitBreakerPolicy>
        {
            new("defaultBreaker", 1, "60s", "60s", "consecutiveFailures > 5"),
        };
        var targetBindings = new List<DaprResiliencyTargetBinding>
        {
            new("eventstore", "App", "Outbound", "defaultRetry", "defaultTimeout", "defaultBreaker"),
        };

        var spec = new DaprResiliencySpec(
            retryPolicies,
            timeoutPolicies,
            circuitBreakerPolicies,
            targetBindings,
            IsConfigurationAvailable: true,
            RawYamlContent: "apiVersion: dapr.io/v1alpha1",
            ErrorMessage: null);

        spec.RetryPolicies.Count.ShouldBe(1);
        spec.TimeoutPolicies.Count.ShouldBe(1);
        spec.CircuitBreakerPolicies.Count.ShouldBe(1);
        spec.TargetBindings.Count.ShouldBe(1);
        spec.IsConfigurationAvailable.ShouldBeTrue();
    }
}
