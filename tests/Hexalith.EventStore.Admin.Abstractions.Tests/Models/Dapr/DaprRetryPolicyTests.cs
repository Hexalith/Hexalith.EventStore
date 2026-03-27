using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Dapr;

public class DaprRetryPolicyTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var policy = new DaprRetryPolicy(
            "defaultRetry",
            "constant",
            3,
            "1s",
            "15s");

        policy.Name.ShouldBe("defaultRetry");
        policy.Strategy.ShouldBe("constant");
        policy.MaxRetries.ShouldBe(3);
        policy.Duration.ShouldBe("1s");
        policy.MaxInterval.ShouldBe("15s");
    }

    [Fact]
    public void Constructor_WithNullDuration_CreatesInstance()
    {
        var policy = new DaprRetryPolicy(
            "exponentialRetry",
            "exponential",
            5,
            null,
            "30s");

        policy.Duration.ShouldBeNull();
        policy.MaxInterval.ShouldBe("30s");
    }

    [Fact]
    public void Constructor_WithNullMaxInterval_CreatesInstance()
    {
        var policy = new DaprRetryPolicy(
            "simpleRetry",
            "constant",
            2,
            "500ms",
            null);

        policy.Duration.ShouldBe("500ms");
        policy.MaxInterval.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithBothNullableFieldsNull_CreatesInstance()
    {
        var policy = new DaprRetryPolicy(
            "minimalRetry",
            "constant",
            1,
            null,
            null);

        policy.Name.ShouldBe("minimalRetry");
        policy.Strategy.ShouldBe("constant");
        policy.MaxRetries.ShouldBe(1);
        policy.Duration.ShouldBeNull();
        policy.MaxInterval.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? name)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprRetryPolicy(
                name!,
                "constant",
                3,
                "1s",
                "15s"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidStrategy_ThrowsArgumentException(string? strategy)
    {
        Should.Throw<ArgumentException>(() =>
            new DaprRetryPolicy(
                "defaultRetry",
                strategy!,
                3,
                "1s",
                "15s"));
    }
}
