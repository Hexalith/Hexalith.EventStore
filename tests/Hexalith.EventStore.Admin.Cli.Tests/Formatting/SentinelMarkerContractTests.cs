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
    [InlineData("https://example.test/health?access_token=")]
    [InlineData("https://example.test/health?sig=")]
    [InlineData("Hexalith.EventStore.Administration")]
    [InlineData("")]
    [InlineData(null)]
    public void BenignTextIsNotFalseFlagged(string? value) => UnsafeMarkerDetection.ContainsUnsafeMarker(value).ShouldBeFalse(
            $"Safe text '{value}' was flagged by UnsafeMarkerDetection.ContainsUnsafeMarker. The marker patterns should require value-bearing key=value shapes, not bare keyword substrings.");

    [Theory]
    [MemberData(nameof(CredentialShapes))]
    public void RealCredentialShapesAreDetected(string value) => UnsafeMarkerDetection.ContainsUnsafeMarker(value).ShouldBeTrue(
            $"Credential-shaped value '{value}' was not detected by UnsafeMarkerDetection.ContainsUnsafeMarker.");

    [Theory]
    [InlineData("access_token")]
    [InlineData("accessToken")]
    [InlineData("refresh_token")]
    [InlineData("refreshToken")]
    [InlineData("id_token")]
    [InlineData("idToken")]
    [InlineData("token")]
    [InlineData("password")]
    [InlineData("secret")]
    [InlineData("client_secret")]
    [InlineData("clientSecret")]
    [InlineData("api_key")]
    [InlineData("apiKey")]
    [InlineData("authorization")]
    public void EverySupportedQuerySecretAliasIsDetected(string alias)
        => UnsafeMarkerDetection.ContainsUnsafeMarker($"https://example.test/health?{alias}=secret-value").ShouldBeTrue();

    public static TheoryData<string> CredentialShapes => new()
    {
        string.Concat("Server=...;Connection", "String=Endpoint=..."),
        string.Concat("Endpoint=sb://example.servicebus.windows.net/;SharedAccess", "Key=foo"),
        string.Concat("Account", "Key=foo"),
        string.Concat("pass", "word=hunter2"),
        "Bearer " + "eyJhbGciOiJIUzI1NiJ9.payload.signature",
        "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhZG1pbiJ9.abcdefgh12345678",
        "eyJhbGciOiJIUzI1NiJ9.IHsic3ViIjoiYWRtaW4ifQ.abcdefgh12345678",
        "IHsiYWxnIjoiSFMyNTYifQ.eyJzdWIiOiJhZG1pbiJ9.abcdefgh12345678",
        "{\"access_token\":\"secret-value\"}",
        "{\"accessToken\":\"secret-value\"}",
        "{\"refreshToken\":\"secret-value\"}",
        "{\"idToken\":\"secret-value\"}",
        "{\"clientSecret\":\"secret-value\"}",
        "{\"apiKey\":\"secret-value\"}",
        "{\"client_secret\":\"secret-value\"}",
        "client_secret" + "=secret-value",
        "https://example.test/health?access_token" + "=secret-value",
        "https://example.test/health?api_key" + "=secret-value",
        "https://example.test/health?password" + "=secret-value",
        "https://example.test/health%3Faccess_token%3Dsecret-value",
        "https://example.test/health?%2561ccess_token=secret-value",
        @"{""\u0070assword"":""secret-value""}",
        "https://operator:" + "password@example.test/path",
        "https://operator%3A" + "password@example.test/path",
        "PROTECTED_marker",
    };
}
