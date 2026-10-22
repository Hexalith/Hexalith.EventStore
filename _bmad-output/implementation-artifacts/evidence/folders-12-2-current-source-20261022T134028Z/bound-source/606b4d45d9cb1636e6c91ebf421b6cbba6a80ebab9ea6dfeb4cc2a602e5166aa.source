using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Security;

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

    [Fact]
    public void CsvFormatter_RedactedContentColumn_RendersSafeDescriptorFields() {
        CsvOutputFormatter formatter = new();
        TestRedactedItem item = new(
            AdminRedactedContent.Protected(
                contentKind: "snapshot-state",
                reasonCode: "key-unavailable",
                stage: "cli-csv",
                metadataVersion: 7,
                retryable: false,
                permanent: true,
                safeNextAction: "Use restore admission status.",
                tenantId: "acme",
                domain: "orders",
                aggregateId: "order-1",
                sequenceNumber: 99,
                correlationId: "corr-99"));
        List<ColumnDefinition> columns = [new("State", nameof(TestRedactedItem.State))];

        string result = formatter.Format(item, columns);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldContain("Protected content redacted.");
        result.ShouldContain("snapshot-state");
        result.ShouldContain("key-unavailable");
        result.ShouldContain("cli-csv");
        result.ShouldContain("metadataVersion=7");
        result.ShouldContain("retryable=false");
        result.ShouldContain("permanent=true");
        result.ShouldContain("Use restore admission status.");
    }

    [Fact]
    public void CsvFormatter_StringValueContainingProtectedSentinel_IsRedacted() {
        CsvOutputFormatter formatter = new();
        TestMessageItem item = new(ProtectedDataLeakSentinel.ProtectedProviderExceptionText);

        string result = formatter.Format(item);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldContain("Protected content redacted.");
    }

    private record TestItem(string Name, int Value);

    private record TestRedactedItem(AdminRedactedContent State);

    private record TestMessageItem(string Message);
}
