using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprCircuitBreakerPolicyTests {
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance() {
        var policy = new DaprCircuitBreakerPolicy(
            "defaultBreaker",
            1,
            "60s",
            "60s",
            "consecutiveFailures > 5");

        policy.Name.ShouldBe("defaultBreaker");
        policy.MaxRequests.ShouldBe(1);
        policy.Interval.ShouldBe("60s");
        policy.Timeout.ShouldBe("60s");
        policy.Trip.ShouldBe("consecutiveFailures > 5");
    }

    [Fact]
    public void Constructor_WithCustomValues_CreatesInstance() {
        var policy = new DaprCircuitBreakerPolicy(
            "aggressiveBreaker",
            5,
            "30s",
            "120s",
            "consecutiveFailures > 3");

        policy.Name.ShouldBe("aggressiveBreaker");
        policy.MaxRequests.ShouldBe(5);
        policy.Interval.ShouldBe("30s");
        policy.Timeout.ShouldBe("120s");
        policy.Trip.ShouldBe("consecutiveFailures > 3");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? name) => Should.Throw<ArgumentException>(() =>
                                                                                              new DaprCircuitBreakerPolicy(
                                                                                                  name!,
                                                                                                  1,
                                                                                                  "60s",
                                                                                                  "60s",
                                                                                                  "consecutiveFailures > 5"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidInterval_ThrowsArgumentException(string? interval) => Should.Throw<ArgumentException>(() =>
                                                                                                      new DaprCircuitBreakerPolicy(
                                                                                                          "defaultBreaker",
                                                                                                          1,
                                                                                                          interval!,
                                                                                                          "60s",
                                                                                                          "consecutiveFailures > 5"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTimeout_ThrowsArgumentException(string? timeout) => Should.Throw<ArgumentException>(() =>
                                                                                                    new DaprCircuitBreakerPolicy(
                                                                                                        "defaultBreaker",
                                                                                                        1,
                                                                                                        "60s",
                                                                                                        timeout!,
                                                                                                        "consecutiveFailures > 5"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidTrip_ThrowsArgumentException(string? trip) => Should.Throw<ArgumentException>(() =>
                                                                                              new DaprCircuitBreakerPolicy(
                                                                                                  "defaultBreaker",
                                                                                                  1,
                                                                                                  "60s",
                                                                                                  "60s",
                                                                                                  trip!));
}
