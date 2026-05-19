using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Cli.Tests.Formatting;

public class TableOutputFormatterTests {
    [Fact]
    public void TableFormatter_Collection_ReturnsAlignedColumns() {
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
    public void TableFormatter_LongValues_AreTruncated() {
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

    [Fact]
    public void TableFormatter_RedactedContentColumn_RendersSafeDescriptorFields() {
        TableOutputFormatter formatter = new();
        TestRedactedItem item = new(
            AdminRedactedContent.Protected(
                contentKind: "event-payload",
                reasonCode: "protected-content",
                stage: "cli-table",
                metadataVersion: 2,
                retryable: true,
                permanent: false,
                safeNextAction: "Inspect protection metadata.",
                tenantId: "acme",
                domain: "orders",
                aggregateId: "order-1",
                sequenceNumber: 42,
                correlationId: "corr-1"));
        List<ColumnDefinition> columns = [new("Payload", nameof(TestRedactedItem.Payload), MaxWidth: 240)];

        string result = formatter.Format(item, columns);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldContain("Protected content redacted.");
        result.ShouldContain("event-payload");
        result.ShouldContain("protected-content");
        result.ShouldContain("cli-table");
        result.ShouldContain("metadataVersion=2");
        result.ShouldContain("retryable=true");
        result.ShouldContain("permanent=false");
        result.ShouldContain("Inspect protection metadata.");
    }

    [Fact]
    public void TableFormatter_StringValueContainingProtectedSentinel_IsRedacted() {
        TableOutputFormatter formatter = new();
        TestMessageItem item = new(ProtectedDataLeakSentinel.ProtectedProviderExceptionText);

        string result = formatter.Format(item);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldContain("Protected content redacted.");
    }

    private record TestItem(string Name, int Value);

    private record TestRedactedItem(AdminRedactedContent Payload);

    private record TestMessageItem(string Message);
}
