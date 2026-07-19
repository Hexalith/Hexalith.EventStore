using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Authorization;

public class DualPrincipalClaimsHelperTests {
    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void PublicCompatibility_PreservesFiveParameterConstructorAndDeconstruct() {
        Type[] priorParameters = [
            typeof(string),
            typeof(string),
            typeof(bool),
            typeof(IReadOnlyList<string>),
            typeof(IReadOnlyList<string>),
        ];
        typeof(DualPrincipalIdentity).GetConstructor(priorParameters).ShouldNotBeNull();
        var value = new DualPrincipalIdentity(
            "actor-1",
            "workload-1",
            true,
            ["orders.read"],
            ["eventstore-api"]);
        (string actorId, string? workloadId, bool delegated, _, _) = value;

        actorId.ShouldBe("actor-1");
        workloadId.ShouldBe("workload-1");
        delegated.ShouldBeTrue();
        value.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Extract_OnlySubClaim_ReturnsOriginalActorIdAndLegacyDefaults() {
        ClaimsPrincipal principal = CreatePrincipal();

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.OriginalActorId.ShouldBe("user-1");
        identity.AuthenticatedWorkloadId.ShouldBeNull();
        identity.IsDelegated.ShouldBeFalse();
        identity.Scopes.ShouldBeNull();
        identity.Audience.ShouldBeNull();
        identity.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Extract_AzpClaimPresent_SetsAuthenticatedWorkloadIdFromAzp() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("azp", "gateway-client"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.AuthenticatedWorkloadId.ShouldBe("gateway-client");
    }

    [Fact]
    public void Extract_AzpAbsentClientIdPresent_FallsBackToClientId() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("client_id", "cli-client"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.AuthenticatedWorkloadId.ShouldBe("cli-client");
    }

    [Fact]
    public void Extract_AzpAndClientIdAbsent_FallsBackToFirstAudEntry() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("aud", "audience-one"),
            new Claim("aud", "audience-two"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.AuthenticatedWorkloadId.ShouldBe("audience-one");
    }

    [Fact]
    public void Extract_NoAzpClientIdOrAud_AuthenticatedWorkloadIdIsNull() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("sub", "user-1"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.AuthenticatedWorkloadId.ShouldBeNull();
    }

    [Fact]
    public void Extract_ActClaimPresent_IsDelegatedTrue() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("act", "{\"sub\":\"delegate-service\"}"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeTrue();
        identity.DelegationId.ShouldBe("delegate-service");
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"sub\":null}")]
    [InlineData("{\"sub\":42}")]
    [InlineData("{\"sub\":\"   \"}")]
    [InlineData("{\"sub\":\"one\",\"sub\":\"two\"}")]
    public void Extract_MalformedOrAmbiguousActSubject_LeavesDelegationIdUnknown(string actClaim) {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("act", actClaim));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeTrue();
        identity.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Extract_OversizedActSubject_LeavesDelegationIdUnknownInsteadOfTruncating() {
        string oversizedSubject = new('d', 513);
        ClaimsPrincipal principal = CreatePrincipal(new Claim(
            "act",
            JsonSerializer.Serialize(new { sub = oversizedSubject })));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeTrue();
        identity.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Extract_MultipleActClaims_LeavesDelegationIdUnknown() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("act", "{\"sub\":\"delegate-one\"}"),
            new Claim("act", "{\"sub\":\"delegate-two\"}"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeTrue();
        identity.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Extract_ActClaimEmptyValue_IsNotTreatedAsPresent() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("act", "   "));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeFalse();
        identity.DelegationId.ShouldBeNull();
    }

    [Fact]
    public void Extract_AzpDiffersFromClientId_IsDelegatedTrue() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("azp", "gateway-client"),
            new Claim("client_id", "backend-client"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeTrue();
        identity.AuthenticatedWorkloadId.ShouldBe("gateway-client");
    }

    [Fact]
    public void Extract_AzpMatchesClientId_IsDelegatedFalse() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("azp", "same-client"),
            new Claim("client_id", "same-client"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeFalse();
    }

    [Fact]
    public void Extract_OnlyAzpPresentNoClientId_IsDelegatedFalse() {
        // No OBO/service-account flow exists in this repository today: without a client_id to
        // compare against, azp alone must not be treated as delegation evidence.
        ClaimsPrincipal principal = CreatePrincipal(new Claim("azp", "gateway-client"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.IsDelegated.ShouldBeFalse();
    }

    [Fact]
    public void Extract_ScopeClaim_SplitsOnWhitespace() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scope", "read write  admin"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Scopes.ShouldBe(["read", "write", "admin"]);
    }

    [Fact]
    public void Extract_ScopeAbsentScpPresent_FallsBackToScp() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scp", "read write"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Scopes.ShouldBe(["read", "write"]);
    }

    [Fact]
    public void Extract_ScopeAndScpBothPresent_PrefersScope() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("scope", "read"),
            new Claim("scp", "write"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Scopes.ShouldBe(["read"]);
    }

    [Fact]
    public void Extract_NoScopeOrScpClaim_ScopesIsNull() {
        ClaimsPrincipal principal = CreatePrincipal();

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Scopes.ShouldBeNull();
    }

    [Fact]
    public void Extract_WhitespaceOnlyScopeClaim_ScopesIsNull() {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scope", "   "));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Scopes.ShouldBeNull();
    }

    [Fact]
    public void Extract_MultipleAudClaims_ReturnsAllInClaimOrder() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("aud", "aud-one"),
            new Claim("aud", "aud-two"),
            new Claim("aud", "aud-three"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Audience.ShouldBe(["aud-one", "aud-two", "aud-three"]);
    }

    [Fact]
    public void Extract_NoAudClaims_AudienceIsNull() {
        ClaimsPrincipal principal = CreatePrincipal();

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Audience.ShouldBeNull();
    }

    [Fact]
    public void Extract_NullPrincipal_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => DualPrincipalClaimsHelper.Extract(null!, "user-1"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_InvalidOriginalActorId_ThrowsArgumentException(string? originalActorId) {
        ClaimsPrincipal principal = CreatePrincipal();

        Should.Throw<ArgumentException>(() => DualPrincipalClaimsHelper.Extract(principal, originalActorId!));
    }

    // Scopes/Audience are sourced from attacker-influenceable claims and threaded onto every
    // QueryEnvelope, not only opted-in safe-denial routes -- bound the list size so a caller
    // cannot inflate every request's serialized payload via an oversized claim.
    [Fact]
    public void Extract_ScopeClaimExceedsMaximum_TruncatesToMaximumEntries() {
        string manyScopes = string.Join(' ', Enumerable.Range(0, 100).Select(i => $"scope-{i}"));
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scope", manyScopes));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        // MaxClaimListEntries = 64 (DualPrincipalClaimsHelper).
        _ = identity.Scopes.ShouldNotBeNull();
        identity.Scopes.Count.ShouldBe(64);
        identity.Scopes[0].ShouldBe("scope-0");
        identity.Scopes[^1].ShouldBe("scope-63");
    }

    [Fact]
    public void Extract_AudClaimsExceedMaximum_TruncatesToMaximumEntries() {
        ClaimsPrincipal principal = CreatePrincipal(
            [.. Enumerable.Range(0, 100).Select(i => new Claim("aud", $"aud-{i}"))]);

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        // MaxClaimListEntries = 64 (DualPrincipalClaimsHelper).
        _ = identity.Audience.ShouldNotBeNull();
        identity.Audience.Count.ShouldBe(64);
        identity.Audience[0].ShouldBe("aud-0");
        identity.Audience[^1].ShouldBe("aud-63");
    }

    [Fact]
    public void Extract_ScopeClaimAtMaximum_PreservesAllEntries() {
        string exactlyMaxScopes = string.Join(' ', Enumerable.Range(0, 64).Select(i => $"scope-{i}"));
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scope", exactlyMaxScopes));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        identity.Scopes.ShouldNotBeNull();
        identity.Scopes!.Count.ShouldBe(64);
    }

    // A single oversized claim value never splits into multiple entries, so it stays within the
    // 64-entry cap (MaxClaimListEntries) while still being arbitrarily large -- the per-entry
    // length bound (MaxClaimValueLength = 512) must close that gap independently of entry count.
    [Fact]
    public void Extract_SingleAudClaimExceedsMaximumLength_TruncatesToMaximumLength() {
        string oversizedAud = new('a', 10_000);
        ClaimsPrincipal principal = CreatePrincipal(new Claim("aud", oversizedAud));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        _ = identity.Audience.ShouldNotBeNull();
        identity.Audience.Count.ShouldBe(1);
        identity.Audience[0].Length.ShouldBe(512);
        identity.Audience[0].ShouldBe(oversizedAud[..512]);
    }

    [Fact]
    public void Extract_SingleScopeTokenExceedsMaximumLength_TruncatesToMaximumLength() {
        // No internal whitespace -> a single scope entry, never split into multiple entries.
        string oversizedScope = new('s', 10_000);
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scope", oversizedScope));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        _ = identity.Scopes.ShouldNotBeNull();
        identity.Scopes.Count.ShouldBe(1);
        identity.Scopes[0].Length.ShouldBe(512);
        identity.Scopes[0].ShouldBe(oversizedScope[..512]);
    }

    [Fact]
    public void Extract_ScopeClaimValueAtMaximumLength_PreservesFullValue() {
        string exactlyMaxScope = new('s', 512);
        ClaimsPrincipal principal = CreatePrincipal(new Claim("scope", exactlyMaxScope));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "user-1");

        _ = identity.Scopes.ShouldNotBeNull();
        identity.Scopes[0].ShouldBe(exactlyMaxScope);
    }

    [Fact]
    public void Extract_FullDualPrincipalToken_PopulatesAllFields() {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("sub", "end-user-1"),
            new Claim("azp", "gateway-client"),
            new Claim("client_id", "backend-client"),
            new Claim("act", "{\"sub\":\"gateway-client\"}"),
            new Claim("scope", "orders.read orders.write"),
            new Claim("aud", "eventstore-api"));

        DualPrincipalIdentity identity = DualPrincipalClaimsHelper.Extract(principal, "end-user-1");

        identity.OriginalActorId.ShouldBe("end-user-1");
        identity.AuthenticatedWorkloadId.ShouldBe("gateway-client");
        identity.IsDelegated.ShouldBeTrue();
        identity.Scopes.ShouldBe(["orders.read", "orders.write"]);
        identity.Audience.ShouldBe(["eventstore-api"]);
        identity.DelegationId.ShouldBe("gateway-client");
    }
}
