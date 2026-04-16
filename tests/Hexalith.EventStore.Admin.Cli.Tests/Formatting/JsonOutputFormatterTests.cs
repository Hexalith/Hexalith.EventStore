using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Tests.Formatting;

public class JsonOutputFormatterTests {
    [Fact]
    public void JsonFormatter_SingleObject_ReturnsIndentedJson() {
        // Arrange
        JsonOutputFormatter formatter = new();
        var item = new { Name = "test", Count = 42 };

        // Act
        string result = formatter.Format(item);

        // Assert
        result.ShouldContain("\"name\"");
        result.ShouldContain("\"count\"");
        result.ShouldContain("42");
        result.ShouldContain("\n"); // indented
    }

    [Fact]
    public void JsonFormatter_Collection_ReturnsJsonArray() {
        // Arrange
        JsonOutputFormatter formatter = new();
        List<object> items = [new { Name = "a" }, new { Name = "b" }];

        // Act
        string result = formatter.FormatCollection(items);

        // Assert
        result.ShouldStartWith("[");
        result.TrimEnd().ShouldEndWith("]");
    }

    [Fact]
    public void JsonFormatter_EnumValues_SerializeAsStrings() {
        // Arrange
        JsonOutputFormatter formatter = new();
        var item = new { Status = HealthStatus.Healthy };

        // Act
        string result = formatter.Format(item);

        // Assert
        result.ShouldContain("\"healthy\"");
        result.ShouldNotContain("\"0\"");
        result.ShouldNotContain(": 0");
    }
}
