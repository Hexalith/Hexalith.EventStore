using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

namespace Hexalith.EventStore.Admin.Abstractions.Tests.Models.Streams;

public class FieldChangeTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        var change = new FieldChange("$.status", "\"open\"", "\"closed\"");

        change.FieldPath.ShouldBe("$.status");
        change.OldValue.ShouldBe("\"open\"");
        change.NewValue.ShouldBe("\"closed\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidFieldPath_ThrowsArgumentException(string? fieldPath)
    {
        Should.Throw<ArgumentException>(() =>
            new FieldChange(fieldPath!, "\"old\"", "\"new\""));
    }

    [Fact]
    public void Constructor_WithNullOldValue_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new FieldChange("$.status", null!, "\"new\""));
    }

    [Fact]
    public void Constructor_WithNullNewValue_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new FieldChange("$.status", "\"old\"", null!));
    }

    [Fact]
    public void ToString_RedactsOldAndNewValues()
    {
        var change = new FieldChange("$.status", "\"secret-old\"", "\"secret-new\"");

        string result = change.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldContain("$.status");
        result.ShouldNotContain("secret-old");
        result.ShouldNotContain("secret-new");
    }
}
