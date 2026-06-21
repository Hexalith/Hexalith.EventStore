using System.Text.RegularExpressions;

using Hexalith.EventStore.Testing.Integration;

using Shouldly;

namespace Hexalith.EventStore.Testing.Integration.Tests;

public class DaprTestPrerequisiteDiagnosticsTests {
    [Fact]
    public void SkipReason_NamesDaprPrerequisiteCategoriesWithoutSecrets() {
        string reason = DaprTestPrerequisites.SkipReason;

        reason.ShouldContain("DAPR integration prerequisites are unavailable");
        reason.ShouldContain("dapr init");
        reason.ShouldContain("Redis");
        reason.ShouldContain("placement");
        reason.ShouldContain("scheduler");
        AssertSupportSafe(reason);
    }

    [Fact]
    public void FixturePrerequisiteFailureMessage_NamesDependencyCategoryAndPortOnly() {
        string message = DaprDiagnostics.BuildPrerequisiteFailureMessage(
            [
                $"Redis is not responding to PING on localhost:{DaprDiagnostics.DefaultRedisPort}",
                $"Dapr placement service is not reachable on localhost:{DaprLocalEndpoints.PlacementPort}",
                $"Dapr scheduler service is not reachable on localhost:{DaprLocalEndpoints.SchedulerPort}",
            ]);

        message.ShouldContain("Dapr infrastructure pre-flight check failed");
        message.ShouldContain("dapr init");
        message.ShouldContain("Redis");
        message.ShouldContain($"localhost:{DaprDiagnostics.DefaultRedisPort}");
        message.ShouldContain("placement");
        message.ShouldContain($"localhost:{DaprLocalEndpoints.PlacementPort}");
        message.ShouldContain("scheduler");
        message.ShouldContain($"localhost:{DaprLocalEndpoints.SchedulerPort}");
        AssertSupportSafe(message);
    }

    [Theory]
    [InlineData("daprd exited immediately with code 1.")]
    [InlineData("Dapr sidecar did not become healthy within 60 seconds.")]
    [InlineData("component initialization failed for state.redis.")]
    [InlineData("statestore init timeout while loading actor state store.")]
    public void InfrastructureStartupClassifier_SkipsNarrowDaprStartupFailures(string message) {
        bool result = DaprDiagnostics.IsDaprInfrastructureStartupFailure(new InvalidOperationException(message));

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Domain processing failed for command type CreateTenant.")]
    [InlineData("The statestore actor write failed after a processed command.")]
    [InlineData("Service invocation to /process returned a product error.")]
    [InlineData("Tenant aggregate rejected duplicate command input.")]
    public void InfrastructureStartupClassifier_DoesNotSkipProductFailures(string message) {
        bool result = DaprDiagnostics.IsDaprInfrastructureStartupFailure(new InvalidOperationException(message));

        result.ShouldBeFalse();
    }

    [Fact]
    public void SupportSafeDiagnostic_RedactsSecretsTokensAndPrivateAddresses() {
        string diagnostic = DaprDiagnostics.ToSupportSafeDiagnostic(
            "Bearer abcdefghijklmnopqrstuvwxyz12345 eyJheader.payload.signature Password=s3cr3t AccountKey=abc123 redis://cache.local:6379 10.1.2.3 "
            + "issuer=https://identity.internal.example/realms/hexalith tenantId='tenant-prod-001' userId=\"real-user\" email=real-user@example.com");

        diagnostic.ShouldContain("[redacted-token]");
        diagnostic.ShouldContain("[redacted-jwt]");
        diagnostic.ShouldContain("[redacted-secret]");
        diagnostic.ShouldContain("[redacted-connection]");
        diagnostic.ShouldContain("[redacted-private-address]");
        diagnostic.ShouldContain("[redacted-url]");
        diagnostic.ShouldContain("tenantId=[redacted-id]");
        diagnostic.ShouldContain("userId=[redacted-id]");
        diagnostic.ShouldNotContain("tenant-prod-001");
        diagnostic.ShouldNotContain("real-user");
        diagnostic.ShouldContain("[redacted-email]");
        AssertSupportSafe(diagnostic);
    }

    [Fact]
    public void SupportSafeProcessDiagnostic_NamesCommandCategoryWithoutRawException() {
        const string diagnostic = "Domain processing failed for command type CreateTenant.";

        diagnostic.ShouldContain("CreateTenant");
        diagnostic.ShouldNotContain("Exception");
        diagnostic.ShouldNotContain("Payload");
        AssertSupportSafe(diagnostic);
    }

    [Fact]
    public void DependencyDiagnosticCategories_AreSupportSafeWhenRecordedAsEvidence() {
        string[] categories =
        [
            "DAPR state store",
            "DAPR sidecar",
            $"Redis localhost:{DaprDiagnostics.DefaultRedisPort}",
            $"placement localhost:{DaprLocalEndpoints.PlacementPort}",
            $"scheduler localhost:{DaprLocalEndpoints.SchedulerPort}",
            "EventStore command gateway",
            "Tenants query route",
            "service invocation boundary",
        ];

        foreach (string category in categories) {
            AssertSupportSafe(category);
        }
    }

    private static void AssertSupportSafe(string value) {
        Regex compactJwt = new(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.Compiled);
        Regex bearerToken = new(@"Bearer\s+[A-Za-z0-9._~+/=-]{20,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
            @"\b(?:tenantId|tenant|userId|user|sub|subject)\s*[:=]\s*['""]?[A-Za-z0-9._@%+-]{3,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        compactJwt.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include compact JWTs.");
        bearerToken.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include bearer tokens.");
        connectionString.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include concrete connection strings.");
        rawPrivateAddress.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include private network addresses.");
        realIssuerUrl.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include real issuer URLs.");
        email.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include email addresses or PII.");
        concreteTenantOrUserId.IsMatch(value).ShouldBeFalse("DAPR prerequisite diagnostics must not include concrete tenant or user IDs.");
    }
}
