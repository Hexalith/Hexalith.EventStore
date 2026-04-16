using Hexalith.EventStore.Admin.Server.Helpers;

namespace Hexalith.EventStore.Admin.Server.Tests.Helpers;

public class AdminStateStoreKeysTests {
    [Fact]
    public void CommandStatusKey_ReturnsCorrectFormat() {
        string result = AdminStateStoreKeys.CommandStatusKey("tenant1", "corr-123");
        result.ShouldBe("tenant1:corr-123:status");
    }

    [Fact]
    public void CommandArchiveKey_ReturnsCorrectFormat() {
        string result = AdminStateStoreKeys.CommandArchiveKey("tenant1", "corr-123");
        result.ShouldBe("tenant1:corr-123:command");
    }

    [Fact]
    public void CommandStatusKey_WithSpecialCharacters_PreservesInput() {
        string result = AdminStateStoreKeys.CommandStatusKey("tenant-a", "abc-def-ghi");
        result.ShouldBe("tenant-a:abc-def-ghi:status");
    }

    [Fact]
    public void CommandArchiveKey_WithSpecialCharacters_PreservesInput() {
        string result = AdminStateStoreKeys.CommandArchiveKey("tenant-a", "abc-def-ghi");
        result.ShouldBe("tenant-a:abc-def-ghi:command");
    }

    [Theory]
    [InlineData(null, "corr-1")]
    [InlineData("", "corr-1")]
    [InlineData(" ", "corr-1")]
    [InlineData("tenant1", null)]
    [InlineData("tenant1", "")]
    [InlineData("tenant1", " ")]
    public void CommandStatusKey_ThrowsOnNullOrWhitespace(string? tenantId, string? correlationId) => Should.Throw<ArgumentException>(() => AdminStateStoreKeys.CommandStatusKey(tenantId!, correlationId!));

    [Theory]
    [InlineData(null, "corr-1")]
    [InlineData("", "corr-1")]
    [InlineData(" ", "corr-1")]
    [InlineData("tenant1", null)]
    [InlineData("tenant1", "")]
    [InlineData("tenant1", " ")]
    public void CommandArchiveKey_ThrowsOnNullOrWhitespace(string? tenantId, string? correlationId) => Should.Throw<ArgumentException>(() => AdminStateStoreKeys.CommandArchiveKey(tenantId!, correlationId!));
}
