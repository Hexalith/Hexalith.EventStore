using Hexalith.EventStore.Admin.Abstractions.Security;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.Admin.Cli.Tests.Formatting;

/// <summary>
/// P5 — pins the contract between the redaction marker vocabulary (UnsafeMarkerDetection) and the
/// no-leak sentinels (ProtectedDataLeakSentinel). Renaming or rotating either side without updating
/// the other would silently make every leak test pass against text the production code does not
/// actually redact. This test prevents that drift.
/// </summary>
public class SentinelMarkerContractTests {
    [Fact]
    public void EverySentinelIsDetectedByContainsUnsafeMarker() {
        foreach (string sentinel in ProtectedDataLeakSentinel.All()) {
            UnsafeMarkerDetection.ContainsUnsafeMarker(sentinel)
                .ShouldBeTrue($"Sentinel '{sentinel}' was not detected by UnsafeMarkerDetection.ContainsUnsafeMarker — the marker vocabulary and the sentinel constants are out of sync.");
        }
    }

    [Theory]
    [InlineData("Inspect connection string status.")]
    [InlineData("Validate the connectionString property of the diagnostic record.")]
    [InlineData("Operator guidance about passwords (none configured).")]
    [InlineData("")]
    [InlineData(null)]
    public void BenignTextIsNotFalseFlagged(string? value) {
        UnsafeMarkerDetection.ContainsUnsafeMarker(value).ShouldBeFalse(
            $"Safe text '{value}' was flagged by UnsafeMarkerDetection.ContainsUnsafeMarker. The marker patterns should require value-bearing key=value shapes, not bare keyword substrings.");
    }

    [Theory]
    [InlineData("Server=...;ConnectionString=Endpoint=...")]
    [InlineData("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKey=foo")]
    [InlineData("AccountKey=foo")]
    [InlineData("password=hunter2")]
    [InlineData("PROTECTED_marker")]
    public void RealCredentialShapesAreDetected(string value) {
        UnsafeMarkerDetection.ContainsUnsafeMarker(value).ShouldBeTrue(
            $"Credential-shaped value '{value}' was not detected by UnsafeMarkerDetection.ContainsUnsafeMarker.");
    }
}
