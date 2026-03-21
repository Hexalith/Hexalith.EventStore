using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Common;

public class AdminOperationResultTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var result = new AdminOperationResult(true, "op-001", "Success", null);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldBe("op-001");
        result.Message.ShouldBe("Success");
        result.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithFailure_CreatesInstance()
    {
        var result = new AdminOperationResult(false, "op-002", "Not found", "NOT_FOUND");

        result.Success.ShouldBeFalse();
        result.OperationId.ShouldBe("op-002");
        result.Message.ShouldBe("Not found");
        result.ErrorCode.ShouldBe("NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidOperationId_ThrowsArgumentException(string? operationId)
    {
        Should.Throw<ArgumentException>(() =>
            new AdminOperationResult(true, operationId!, null, null));
    }

    [Fact]
    public void Constructor_WithNullMessage_Succeeds()
    {
        var result = new AdminOperationResult(true, "op-001", null, null);

        result.Message.ShouldBeNull();
    }
}
