
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

using ContractsEventEnvelope = Hexalith.EventStore.Contracts.Events.EventEnvelope;
using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Story 5.4, Task 7: Payload protection tests (AC #2, #5, #11).
/// Validates that ToString() overrides redact payload and that no log statements reference payload.
/// </summary>
public class PayloadProtectionTests {
    private static readonly byte[] SamplePayload = [0x01, 0x02, 0x03, 0xFF];

    // --- Task 7.2: EventEnvelope (Contracts) ToString excludes payload ---

    [Fact]
    public void EventEnvelope_ToString_DoesNotContainPayload() {
        var metadata = new EventMetadata(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderPlaced", 1, "json");

        var envelope = new ContractsEventEnvelope(metadata, SamplePayload, null);

        string result = envelope.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("System.Byte[]");
        // Verify the actual byte values don't appear
        result.ShouldNotContain("\x01");
    }

    // --- Task 7.3: CommandEnvelope ToString excludes payload ---

    [Fact]
    public void CommandEnvelope_ToString_DoesNotContainPayload() {
        var envelope = new CommandEnvelope(
            "msg-prot-1", "tenant-a", "billing", "aggregate-1", "PlaceOrder",
            SamplePayload, "corr-1", null, "user-1", null);

        string result = envelope.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("System.Byte[]");
    }

    // --- Task 7.4: EventEnvelope ToString contains all metadata fields ---

    [Fact]
    public void EventEnvelope_ToString_ContainsAllMetadataFields() {
        var metadata = new EventMetadata(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 42, 0, DateTimeOffset.UtcNow,
            "corr-123", "cause-456", "user-bob", "2.1.0", "OrderPlaced", 1, "json");

        var envelope = new ContractsEventEnvelope(metadata, SamplePayload, null);

        string result = envelope.ToString();

        // All 11 metadata fields should be present
        result.ShouldContain("aggregate-1");
        result.ShouldContain("tenant-a");
        result.ShouldContain("billing");
        result.ShouldContain("42");
        result.ShouldContain("corr-123");
        result.ShouldContain("cause-456");
        result.ShouldContain("user-bob");
        result.ShouldContain("2.1.0");
        result.ShouldContain("OrderPlaced");
        result.ShouldContain("json");
    }

    [Fact]
    public void CommandEnvelope_ToString_ContainsAllMetadataFields() {
        var envelope = new CommandEnvelope(
            "msg-prot-2", "tenant-a", "billing", "aggregate-1", "PlaceOrder",
            SamplePayload, "corr-123", "cause-456", "user-bob", null);

        string result = envelope.ToString();

        result.ShouldContain("tenant-a");
        result.ShouldContain("billing");
        result.ShouldContain("aggregate-1");
        result.ShouldContain("PlaceOrder");
        result.ShouldContain("corr-123");
        result.ShouldContain("cause-456");
        result.ShouldContain("user-bob");
    }

    // --- Task 7.5: LoggingBehavior never logs payload ---
    // (Static analysis: verify LoggingBehavior.cs source doesn't reference Payload)

    [Fact]
    public void LoggingBehavior_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.CommandApi", "Pipeline", "LoggingBehavior.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");
        string content = File.ReadAllText(sourcePath);

        // Verify no log statements reference Payload
        // Allow references in comments but not in actual logger.Log calls
        string[] lines = content.Split('\n');
        foreach (string line in lines) {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal)) {
                continue;
            }

            if (trimmed.Contains("logger.Log", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogInformation", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogWarning", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogError", StringComparison.OrdinalIgnoreCase)) {
                trimmed.ShouldNotContain("Payload", Case.Insensitive,
                    $"LoggingBehavior log statement references Payload: {trimmed}");
            }
        }
    }

    // --- Task 7.6: AggregateActor source scan ---

    [Fact]
    public void AggregateActor_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.Server", "Actors", "AggregateActor.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");

        VerifyNoPayloadInLogStatements(sourcePath, "AggregateActor");
    }

    // --- Task 7.7: EventPublisher source scan ---

    [Fact]
    public void EventPublisher_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.Server", "Events", "EventPublisher.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");

        VerifyNoPayloadInLogStatements(sourcePath, "EventPublisher");
    }

    // --- Task 7.8: DeadLetterPublisher source scan ---

    [Fact]
    public void DeadLetterPublisher_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.Server", "Events", "DeadLetterPublisher.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");

        VerifyNoPayloadInLogStatements(sourcePath, "DeadLetterPublisher");
    }

    // --- Server EventEnvelope ToString also redacts payload ---

    [Fact]
    public void ServerEventEnvelope_ToString_DoesNotContainPayload() {
        var envelope = new ServerEventEnvelope(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderPlaced", 1, "json",
            SamplePayload, null);

        string result = envelope.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("System.Byte[]");
    }

    // --- Framework-level enforcement: even explicit logging is safe ---

    [Fact]
    public void EventEnvelope_DeveloperLogsFullObject_PayloadIsRedacted() {
        var metadata = new EventMetadata(
            "msg-1", "agg-1", "test-aggregate", "t-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0", "OrderPlaced", 1, "json");

        var envelope = new ContractsEventEnvelope(metadata, SamplePayload, null);

        // Simulate: logger.LogInformation("Event: {Event}", envelope)
        // The {Event} placeholder calls ToString()
        string logOutput = $"Event: {envelope}";

        logOutput.ShouldContain("[REDACTED]");
        logOutput.ShouldNotContain("System.Byte[]");
    }

    [Fact]
    public void CommandEnvelope_DeveloperLogsFullObject_PayloadIsRedacted() {
        var envelope = new CommandEnvelope(
            "msg-prot-3", "t-a", "billing", "agg-1", "PlaceOrder",
            SamplePayload, "corr-1", null, "user-1", null);

        string logOutput = $"Command: {envelope}";

        logOutput.ShouldContain("[REDACTED]");
        logOutput.ShouldNotContain("System.Byte[]");
    }

    // --- Story 5.3 gap-closure: null payload still shows [REDACTED] (SEC-5) ---

    [Fact]
    public void ServerEventEnvelope_ToString_NullPayload_StillRedacts() {
        // Server EventEnvelope accepts null payload (no guard) -- ToString must still redact
        var envelope = new ServerEventEnvelope(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderPlaced", 1, "json",
            null!, null);

        string result = envelope.ToString();

        result.ShouldContain("[REDACTED]");
        result.ShouldNotContain("System.Byte[]");
    }

    private static void VerifyNoPayloadInLogStatements(string sourcePath, string className) {
        string content = File.ReadAllText(sourcePath);
        string[] lines = content.Split('\n');

        foreach (string line in lines) {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal)) {
                continue;
            }

            if (trimmed.Contains("logger.Log", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogInformation", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogWarning", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogError", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("LogDebug", StringComparison.OrdinalIgnoreCase)) {
                trimmed.ShouldNotContain("Payload", Case.Insensitive,
                    $"{className} log statement references Payload: {trimmed}");
            }
        }
    }
}
