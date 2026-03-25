using Hexalith.EventStore.Admin.Cli.Formatting;

namespace Hexalith.EventStore.Admin.Cli.Tests.Formatting;

public class TableOutputFormatterTests
{
    [Fact]
    public void TableFormatter_Collection_ReturnsAlignedColumns()
    {
        // Arrange
        TableOutputFormatter formatter = new();
        List<TestItem> items = [new("Alpha", 1), new("Beta", 2)];

        // Act
        string result = formatter.FormatCollection(items);

        // Assert
        string[] lines = result.Split(Environment.NewLine);
        lines.Length.ShouldBeGreaterThanOrEqualTo(4); // header + separator + 2 rows
        lines[0].ShouldContain("Name");
        lines[0].ShouldContain("Value");
        lines[1].ShouldContain("---"); // separator
        lines[2].ShouldContain("Alpha");
        lines[3].ShouldContain("Beta");
    }

    [Fact]
    public void TableFormatter_LongValues_AreTruncated()
    {
        // Arrange
        TableOutputFormatter formatter = new();
        List<TestItem> items = [new("VeryLongValueThatExceedsTheLimit", 1)];
        List<ColumnDefinition> columns =
        [
            new("Name", "Name", MaxWidth: 10),
            new("Value", "Value"),
        ];

        // Act
        string result = formatter.FormatCollection(items, columns);

        // Assert
        string[] lines = result.Split(Environment.NewLine);
        // Data row should contain truncated value with "..."
        lines[2].ShouldContain("...");
        // Should not contain the full original value
        lines[2].ShouldNotContain("VeryLongValueThatExceedsTheLimit");
    }

    private record TestItem(string Name, int Value);
}
