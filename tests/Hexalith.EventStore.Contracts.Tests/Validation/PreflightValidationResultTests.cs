
using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.Contracts.Tests.Validation;

public class PreflightValidationResultTests
{
    [Fact]
    public void Constructor_Authorized_CreatesInstance()
    {
        var result = new PreflightValidationResult(IsAuthorized: true);

        Assert.True(result.IsAuthorized);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Constructor_Unauthorized_WithReason()
    {
        var result = new PreflightValidationResult(
            IsAuthorized: false,
            Reason: "Not authorized for tenant 'acme'.");

        Assert.False(result.IsAuthorized);
        Assert.Equal("Not authorized for tenant 'acme'.", result.Reason);
    }

    [Fact]
    public void Constructor_WithoutReason_DefaultsToNull()
    {
        var result = new PreflightValidationResult(IsAuthorized: false);

        Assert.Null(result.Reason);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var result1 = new PreflightValidationResult(true);
        var result2 = new PreflightValidationResult(true);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var result1 = new PreflightValidationResult(true);
        var result2 = new PreflightValidationResult(false, "Denied");

        Assert.NotEqual(result1, result2);
    }
}
