
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;

using Shouldly;

using ContractsEventEnvelope = Hexalith.EventStore.Contracts.Events.EventEnvelope;
using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Story 5.3 review follow-up tests for payload redaction and log-message hygiene.
/// </summary>
public class PayloadProtectionTests {
    private static readonly byte[] SamplePayload = [0x01, 0x02, 0x03, 0xFF];

    // --- Contracts EventEnvelope ToString excludes payload ---

    [Fact]
    public void EventEnvelope_ToString_DoesNotContainPayload() {
        var metadata = new EventMetadata(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderPlaced", 1, "json");

        var envelope = new ContractsEventEnvelope(metadata, SamplePayload, null);

        string result = envelope.ToString();

        AssertPayloadIsRedacted(result);
    }

    // --- CommandEnvelope ToString excludes payload ---

    [Fact]
    public void CommandEnvelope_ToString_DoesNotContainPayload() {
        var envelope = new CommandEnvelope(
            "msg-prot-1", "tenant-a", "billing", "aggregate-1", "PlaceOrder",
            SamplePayload, "corr-1", null, "user-1", null);

        string result = envelope.ToString();

        AssertPayloadIsRedacted(result);
    }

    // --- EventEnvelope ToString contains the expected metadata fields ---

    [Fact]
    public void EventEnvelope_ToString_ContainsAllMetadataFields() {
        var metadata = new EventMetadata(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 42, 0, DateTimeOffset.UtcNow,
            "corr-123", "cause-456", "user-bob", "2.1.0", "OrderPlaced", 1, "json");

        var envelope = new ContractsEventEnvelope(metadata, SamplePayload, null);

        string result = envelope.ToString();

        // Key metadata fields should remain visible while the payload stays redacted.
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
        AssertPayloadIsRedacted(result);
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
        AssertPayloadIsRedacted(result);
    }

    // --- LoggingBehavior log templates and direct log calls never reference payload ---

    [Fact]
    public void LoggingBehavior_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore", "Pipeline", "LoggingBehavior.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");

        VerifyNoPayloadInLogStatements(sourcePath, "LoggingBehavior");
    }

    // --- AggregateActor source scan ---

    [Fact]
    public void AggregateActor_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.Server", "Actors", "AggregateActor.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");

        VerifyNoPayloadInLogStatements(sourcePath, "AggregateActor");
    }

    // --- EventPublisher source scan ---

    [Fact]
    public void EventPublisher_AllLogStatements_NeverReferencePayload() {
        string sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Hexalith.EventStore.Server", "Events", "EventPublisher.cs"));

        File.Exists(sourcePath).ShouldBeTrue($"Source file not found: {sourcePath}");

        VerifyNoPayloadInLogStatements(sourcePath, "EventPublisher");
    }

    // --- DeadLetterPublisher source scan ---

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

        AssertPayloadIsRedacted(result);
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

        AssertPayloadIsRedacted(logOutput);
    }

    [Fact]
    public void CommandEnvelope_DeveloperLogsFullObject_PayloadIsRedacted() {
        var envelope = new CommandEnvelope(
            "msg-prot-3", "t-a", "billing", "agg-1", "PlaceOrder",
            SamplePayload, "corr-1", null, "user-1", null);

        string logOutput = $"Command: {envelope}";

        AssertPayloadIsRedacted(logOutput);
    }

    // --- Null payload still renders as redacted ---

    [Fact]
    public void ServerEventEnvelope_ToString_NullPayload_StillRedacts() {
        // Server EventEnvelope accepts null payload (no guard) -- ToString must still redact
        var envelope = new ServerEventEnvelope(
            "msg-1", "aggregate-1", "test-aggregate", "tenant-a", "billing", 1, 0, DateTimeOffset.UtcNow,
            "corr-1", "cause-1", "user-1", "1.0.0", "OrderPlaced", 1, "json",
            null!, null);

        string result = envelope.ToString();

        AssertPayloadIsRedacted(result);
    }

    private static void AssertPayloadIsRedacted(string renderedValue) {
        renderedValue.ShouldContain("[REDACTED]");

        foreach (string representation in GetCommonPayloadRepresentations(SamplePayload)) {
            renderedValue.ShouldNotContain(
                representation,
                Case.Sensitive,
                $"Rendered value leaked payload representation '{representation}': {renderedValue}");
        }
    }

    private static void VerifyNoPayloadInLogStatements(string sourcePath, string className) {
        string sanitizedContent = StripComments(File.ReadAllText(sourcePath));

        foreach (string candidate in EnumerateLogStatements(sanitizedContent)) {
            candidate.ShouldNotContain(
                "Payload",
                Case.Insensitive,
                $"{className} log statement references payload: {candidate}");
        }
    }

    private static IEnumerable<string> GetCommonPayloadRepresentations(byte[] payload) {
        yield return "System.Byte[]";
        yield return Convert.ToBase64String(payload);
        yield return Convert.ToHexString(payload);
        yield return Convert.ToHexString(payload).ToLowerInvariant();

        string[] decimalValues = [.. payload.Select(static b => b.ToString(CultureInfo.InvariantCulture))];
        yield return string.Join(",", decimalValues);
        yield return string.Join(", ", decimalValues);

        string[] hexValues = [.. payload.Select(static b => b.ToString("X2", CultureInfo.InvariantCulture))];
        yield return string.Join("-", hexValues);
        yield return string.Join(" ", hexValues);

        string utf8Payload = Encoding.UTF8.GetString(payload);
        if (!string.IsNullOrWhiteSpace(utf8Payload)) {
            yield return utf8Payload;
        }
    }

    private static IEnumerable<string> EnumerateLogStatements(string source) {
        string[] lines = source.Split(["\r\n", "\n"], StringSplitOptions.None);
        var builder = new StringBuilder();
        bool capturing = false;
        bool loggerMessageAttribute = false;
        int parenthesisBalance = 0;

        foreach (string line in lines) {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) {
                continue;
            }

            if (!capturing && IsLogStatementStart(trimmed, out loggerMessageAttribute)) {
                _ = builder.Clear();
                _ = builder.Append(trimmed);
                parenthesisBalance = CountParenthesisBalance(trimmed);
                capturing = true;

                if (IsLogStatementComplete(trimmed, loggerMessageAttribute, parenthesisBalance)) {
                    yield return builder.ToString();
                    _ = builder.Clear();
                    capturing = false;
                }

                continue;
            }

            if (!capturing) {
                continue;
            }

            _ = builder.Append(' ').Append(trimmed);
            parenthesisBalance += CountParenthesisBalance(trimmed);

            if (IsLogStatementComplete(trimmed, loggerMessageAttribute, parenthesisBalance)) {
                yield return builder.ToString();
                _ = builder.Clear();
                capturing = false;
            }
        }
    }

    private static bool IsLogStatementStart(string trimmedLine, out bool loggerMessageAttribute) {
        loggerMessageAttribute = trimmedLine.Contains("[LoggerMessage(", StringComparison.Ordinal);
        if (loggerMessageAttribute) {
            return true;
        }

        return trimmedLine.Contains(".LogDebug(", StringComparison.Ordinal)
            || trimmedLine.Contains(".LogInformation(", StringComparison.Ordinal)
            || trimmedLine.Contains(".LogWarning(", StringComparison.Ordinal)
            || trimmedLine.Contains(".LogError(", StringComparison.Ordinal)
            || trimmedLine.Contains(".LogCritical(", StringComparison.Ordinal)
            || trimmedLine.Contains(".LogTrace(", StringComparison.Ordinal)
            || trimmedLine.Contains(".Log(", StringComparison.Ordinal);
    }

    private static bool IsLogStatementComplete(string trimmedLine, bool loggerMessageAttribute, int parenthesisBalance) =>
        loggerMessageAttribute
            ? trimmedLine.Contains(")]", StringComparison.Ordinal) || parenthesisBalance <= 0
            : trimmedLine.Contains(");", StringComparison.Ordinal) && parenthesisBalance <= 0;

    private static int CountParenthesisBalance(string value) =>
        value.Count(static c => c == '(') - value.Count(static c => c == ')');

    private static string StripComments(string source) {
        var builder = new StringBuilder(source.Length);
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool escaping = false;

        for (int i = 0; i < source.Length; i++) {
            char current = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (inLineComment) {
                if (current is '\r' or '\n') {
                    inLineComment = false;
                    _ = builder.Append(current);
                }

                continue;
            }

            if (inBlockComment) {
                if (current == '*' && next == '/') {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (inString) {
                _ = builder.Append(current);

                if (inVerbatimString) {
                    if (current == '"' && next == '"') {
                        _ = builder.Append(next);
                        i++;
                        continue;
                    }

                    if (current == '"') {
                        inString = false;
                        inVerbatimString = false;
                    }
                }
                else {
                    if (!escaping && current == '"') {
                        inString = false;
                    }

                    escaping = !escaping && current == '\\';
                    if (current != '\\') {
                        escaping = false;
                    }
                }

                continue;
            }

            if (current == '@' && next == '"') {
                inString = true;
                inVerbatimString = true;
                _ = builder.Append(current);
                _ = builder.Append(next);
                i++;
                continue;
            }

            if (current == '"') {
                inString = true;
                escaping = false;
                _ = builder.Append(current);
                continue;
            }

            if (current == '/' && next == '/') {
                inLineComment = true;
                i++;
                continue;
            }

            if (current == '/' && next == '*') {
                inBlockComment = true;
                i++;
                continue;
            }

            _ = builder.Append(current);
        }

        return Regex.Replace(builder.ToString(), @"^\s+$", string.Empty, RegexOptions.Multiline);
    }
}
