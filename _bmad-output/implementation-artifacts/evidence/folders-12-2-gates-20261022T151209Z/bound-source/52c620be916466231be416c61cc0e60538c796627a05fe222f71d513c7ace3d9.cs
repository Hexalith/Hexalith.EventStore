using Hexalith.EventStore.Admin.Abstractions.Models;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Cli.Formatting;
using Hexalith.EventStore.Testing.Security;

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

    [Fact]
    public void JsonFormatter_RedactedEventDetail_OmitsRawPayloadJsonAndKeepsDescriptor() {
        JsonOutputFormatter formatter = new();
        var detail = EventDetail.WithRedactedPayload(
            "acme",
            "orders",
            "order-1",
            42,
            "OrderCreated",
            DateTimeOffset.UtcNow,
            "corr-1",
            "caus-1",
            "user-1",
            AdminRedactedContent.Protected(
                contentKind: "event-payload",
                reasonCode: "protected-content",
                stage: "cli-json",
                metadataVersion: 1,
                retryable: false,
                permanent: false,
                safeNextAction: "Inspect protection metadata."));

        string result = formatter.Format(detail);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldNotContain("payloadJson");
        result.ShouldContain("payload");
        result.ShouldContain("Protected content redacted.");
        result.ShouldContain("event-payload");
        result.ShouldContain("protected-content");
    }

    [Fact]
    public void JsonFormatter_StringValueContainingProtectedSentinel_IsRedacted() {
        JsonOutputFormatter formatter = new();
        var item = new { Message = ProtectedDataLeakSentinel.ProtectedProviderExceptionText };

        string result = formatter.Format(item);

        ProtectedDataLeakSentinel.AssertNoLeak([result]);
        result.ShouldContain("Protected content redacted.");
    }
}
