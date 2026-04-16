using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Tests.Formatting;

public class CsvOutputFormatterTests {
    [Fact]
    public void CsvFormatter_Collection_ReturnsHeaderAndRows() {
        // Arrange
        CsvOutputFormatter formatter = new();
        List<TestItem> items = [new("Alpha", 1), new("Beta", 2)];

        // Act
        string result = formatter.FormatCollection(items);

        // Assert
        string[] lines = result.Split(Environment.NewLine);
        lines.Length.ShouldBeGreaterThanOrEqualTo(3);
        lines[0].ShouldBe("Name,Value");
        lines[1].ShouldBe("Alpha,1");
        lines[2].ShouldBe("Beta,2");
    }

    [Fact]
    public void CsvFormatter_ValuesWithCommas_AreQuoted() {
        // Arrange
        CsvOutputFormatter formatter = new();
        List<TestItem> items = [new("Hello, World", 1)];

        // Act
        string result = formatter.FormatCollection(items);

        // Assert
        string[] lines = result.Split(Environment.NewLine);
        lines[1].ShouldContain("\"Hello, World\"");
    }

    [Fact]
    public void CsvFormatter_SingleObject_ReturnsKeyValuePairs() {
        // Arrange
        CsvOutputFormatter formatter = new();
        TestItem item = new("Test", 42);

        // Act
        string result = formatter.Format(item);

        // Assert
        string[] lines = result.Split(Environment.NewLine);
        lines[0].ShouldBe("Property,Value");
        lines.ShouldContain("Name,Test");
        lines.ShouldContain("Value,42");
    }

    private record TestItem(string Name, int Value);
}
