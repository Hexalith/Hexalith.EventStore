using System.Text.RegularExpressions;

using Hexalith.EventStore.Testing.Integration;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

/// <summary>
/// Locks the support-safe diagnostic contract that the local generated-API smoke preflight
/// (<c>scripts/generated-api-smoke-preflight.sh</c>) depends on. The script's Bash <c>redact()</c>
/// mirrors these exact categories; these tests reuse the shared
/// <see cref="DaprDiagnostics"/> helper so both sides stay aligned to one implementation.
/// </summary>
public class GeneratedApiSmokePreflightDiagnosticsTests {
    [Theory]
    [InlineData("environment")]
    [InlineData("aspire")]
    [InlineData("dapr")]
    [InlineData("generated-api")]
    [InlineData("state-evidence")]
    [InlineData("next-action")]
    public void PreflightOutputCategories_AreSupportSafe(string category) {
        // The categories the preflight prints must themselves never carry secrets.
        AssertSupportSafe(category);
        DaprDiagnostics.ToSupportSafeDiagnostic(category).ShouldBe(category);
    }

    [Theory]
    [InlineData("DAPR placement reachable on localhost:50005")]
    [InlineData("DAPR scheduler reachable on localhost:50006")]
    [InlineData("app id eventstore, metadata ok, actor hostReady=true, placement connected")]
    [InlineData("actor host not ready: placement disconnected (actor calls will hang ~60s)")]
    [InlineData("POST .../increment -> 202, Location present, Retry-After=1")]
    [InlineData("GET . -> 200 with ETag")]
    [InlineData("state store holds 3 key(s) for the smoke counter, consistent with the accepted command")]
    [InlineData("smoke not requested; pass --sample-api-smoke to exercise the generated Sample API")]
    public void PreflightDiagnosticLines_StaySupportSafe(string line) {
        // Representative lines the preflight emits must pass the shared support-safe contract unchanged.
        AssertSupportSafe(line);
        DaprDiagnostics.ToSupportSafeDiagnostic(line).ShouldBe(line);
    }

    [Fact]
    public void SharedRedactor_ScrubsSecretShapesThePreflightCanEncounter() {
        string scrubbed = DaprDiagnostics.ToSupportSafeDiagnostic(
            "Authorization: Bearer abcdefghijklmnopqrstuvwxyz12345 eyJhbGci.eyJzdWIi.c2ln "
            + "dapr-api-token=SUPERSECRETTOKENVALUE DAPR_API_TOKEN=ANOTHERSECRET "
            + "Password=s3cr3t redis://cache.internal:6379 10.1.2.3 "
            + "issuer=https://identity.internal.example/realms/hexalith "
            + "tenantId='tenant-prod-001' email=real-user@example.com");

        scrubbed.ShouldContain("Bearer [redacted-token]");
        scrubbed.ShouldContain("[redacted-jwt]");
        scrubbed.ShouldContain("dapr-api-token=[redacted-token]");
        scrubbed.ShouldContain("DAPR_API_TOKEN=[redacted-token]");
        scrubbed.ShouldContain("[redacted-secret]");
        scrubbed.ShouldContain("[redacted-connection]");
        scrubbed.ShouldContain("[redacted-private-address]");
        scrubbed.ShouldContain("[redacted-url]");
        scrubbed.ShouldContain("tenantId=[redacted-id]");
        scrubbed.ShouldContain("[redacted-email]");

        scrubbed.ShouldNotContain("SUPERSECRETTOKENVALUE");
        scrubbed.ShouldNotContain("ANOTHERSECRET");
        scrubbed.ShouldNotContain("tenant-prod-001");
        scrubbed.ShouldNotContain("s3cr3t");
        scrubbed.ShouldNotContain("identity.internal.example");
        AssertSupportSafe(scrubbed);
    }

    [Fact]
    public void SharedRedactor_PreservesLocalEndpointsAndDevFixtures() {
        // Localhost URLs and the known dev fixtures the smoke uses must survive redaction so the
        // preflight can report the endpoints it exercised.
        const string line = "GET http://localhost:8080/api/tenant-a/counter/counter-1 -> 200";
        string scrubbed = DaprDiagnostics.ToSupportSafeDiagnostic(line);

        scrubbed.ShouldBe(line);
        scrubbed.ShouldContain("http://localhost:8080/api/tenant-a/counter/counter-1");
    }

    [Theory]
    [InlineData("daprd exited immediately with code 1.")]
    [InlineData("Dapr sidecar did not become healthy within 60 seconds.")]
    public void InfrastructureClassifier_TreatsControlPlaneStartupAsInfrastructure(string message) {
        // The preflight relies on the shared classifier to keep control-plane startup failures out of
        // the "product defect" bucket.
        DaprDiagnostics.IsDaprInfrastructureStartupFailure(new InvalidOperationException(message))
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData("POST /api/tenant-a/counter/counter-1/increment returned 404.")]
    [InlineData("Generated command route not served by the sample-api host.")]
    public void InfrastructureClassifier_DoesNotHideGeneratedApiProductFailures(string message) {
        DaprDiagnostics.IsDaprInfrastructureStartupFailure(new InvalidOperationException(message))
            .ShouldBeFalse();
    }

    [Fact]
    public void PlacementAndSchedulerPorts_MatchThePreflightCandidatePorts() {
        // The script probes the same candidate ports (6050/50005, 6060/50006) as the shared endpoint
        // resolver, so the resolved ports must land on those documented candidates.
        DaprLocalEndpoints.PlacementPort.ShouldBeOneOf(6050, 50005);
        DaprLocalEndpoints.SchedulerPort.ShouldBeOneOf(6060, 50006);
    }

    private static void AssertSupportSafe(string value) {
        Regex compactJwt = new(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.Compiled);
        Regex bearerToken = new(@"Bearer\s+[A-Za-z0-9._~+/=-]{20,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex daprApiToken = new(@"dapr[_-]?api[_-]?token\s*[:=]\s*(?!\[redacted)[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex connectionString = new(
            @"(AccountKey=|SharedAccessKey=|Password=[^{}\s]|redis://|amqp://|Endpoint=sb://)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex rawPrivateAddress = new(
            @"(?<!localhost:)(?<!127\.0\.0\.1:)\b(10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3})\b",
            RegexOptions.Compiled);
        Regex realIssuerUrl = new(
            @"https?://(?!(?:localhost|127\.0\.0\.1)(?::|/|\b))[^\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex email = new(
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        Regex concreteTenantOrUserId = new(
            @"\b(?:tenantId|tenant|userId|user|sub|subject)\s*[:=]\s*['""]?(?!\[redacted)[A-Za-z0-9._@%+-]{3,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        compactJwt.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include compact JWTs.");
        bearerToken.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include bearer tokens.");
        daprApiToken.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include DAPR API tokens.");
        connectionString.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include connection strings.");
        rawPrivateAddress.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include private network addresses.");
        realIssuerUrl.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include real issuer URLs.");
        email.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include email addresses.");
        concreteTenantOrUserId.IsMatch(value).ShouldBeFalse("preflight diagnostics must not include concrete tenant or user IDs.");
    }
}
