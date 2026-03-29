
using Hexalith.EventStore.Contracts.Validation;

namespace Hexalith.EventStore.Contracts.Tests.Validation;

public class PreflightValidationResultTests {
    [Fact]
    public void Constructor_Authorized_CreatesInstance() {
        var result = new PreflightValidationResult(IsAuthorized: true);

        result.IsAuthorized.ShouldBeTrue();
        result.Reason.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Unauthorized_WithReason() {
        var result = new PreflightValidationResult(
            IsAuthorized: false,
            Reason: "Not authorized for tenant 'acme'.");

        result.IsAuthorized.ShouldBeFalse();
        result.Reason.ShouldBe("Not authorized for tenant 'acme'.");
    }

    [Fact]
    public void Constructor_WithoutReason_DefaultsToNull() {
        var result = new PreflightValidationResult(IsAuthorized: false);

        result.Reason.ShouldBeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual() {
        var result1 = new PreflightValidationResult(true);
        var result2 = new PreflightValidationResult(true);

        result2.ShouldBe(result1);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual() {
        var result1 = new PreflightValidationResult(true);
        var result2 = new PreflightValidationResult(false, "Denied");

        result2.ShouldNotBe(result1);
    }
}
